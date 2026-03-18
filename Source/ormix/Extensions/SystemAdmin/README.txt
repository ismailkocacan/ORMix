Informix İzleme/Gözleme (Monitoring) Tabloları Hakkında Malutmat.

Monitoring tabloları ve View`ların iç yapıları hakkında detaylı bilgi içeren siteler:
https://www.oninit.com/sql2html/sysmaster.html
https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=database-system-monitoring-interface-tables


Her session/connection sunucuda bir Thread(sysrstcb) oluşturmaktadır.
Imlicit ve Explicit transaction`lara ait malumatlar systxptab tablosunda saklanmaktadır.
Transaction`ların kilitlediği kayıt ve tablolara ait bilgiler syslcktab tablosunda saklanmaktadır.


Veritabanı nesneleri arasındaki yapı şu şekildir.

-- Session
--- Thread
----- Transaction
------ Lock


-- Thread               => Transaction          => Lock
-- sysmaster:sysrstcb   => sysmaster:systxptab  => sysmaster:syslcktab


------------------------------------------------------------------------------------------------------------------
Örnek sorgular:
sid = Session id

52176509 numaralı Session id`ye ait Transaction`larin listesinin elde edilmesi.
select * from sysmaster:systxptab where owner in (select address from sysmaster:sysrstcb where sid = 52176509) 


52243567 numaralı Session id`ye ait kilitlenen tablo ve kayıt bilgilerinin elde edilmesi.
select 'owner' as type
       ,systab.tabname as tabname,* 
from sysmaster:syslcktab lcktab
left join sysmaster:systabnames as systab on lcktab.partnum = systab.partnum
where lcktab.owner in (select address from sysmaster:systxptab 
									  where owner in (select address from sysmaster:sysrstcb where sid = 52243567)) 



 
select 
	 owner   --  Owner alanı kaydı kilitleyen transactiona nesnesinin adresini içerir.
	,wtlist  --  wtlist alanı bekleyen thread`lerin adresini içerir. 
	,* 
from sysmaster:syslcktab 
limit 10;



Bu sorgu kilitler hakkında detaylı bilgiler içerir. 
Kilitler serbest bırakıldığı durumlarda wait_thread.sid as waiter alanı boş gelecektir. 
select  
	  txptab.txid
	, session.sid as owner
	, wait_thread.sid as waiter
	, session.pid
	, (current - dbinfo('utc_to_datetime', session.connected)) :: interval hour(4) to second as connection_duration 
	, trim(session.hostname) as hostname
	, trim(session.username) as username
	, trim(tabname.dbsname) as dbsname
	, trim(tabname.tabname) as tabname
	, decode(lcktab.rowidn,0,'T','R') || flags_txt.txt[1,3] as type
	, (current - dbinfo('utc_to_datetime', lcktab.grtime)) :: interval hour(4) to second as lock_duration
	, session.feprogram
	, sysadmin:task('onstat','-g sql '||session.sid) as onstat_session_sql
	, sysadmin:task('onstat','-g ses '||session.sid) as onstat_monitor_session
from    

	sysmaster:syslcktab        as lcktab,
	sysmaster:systabnames      as tabname,
	sysmaster:systxptab        as txptab,
	sysmaster:sysrstcb         as lock_thread,
	sysmaster:flags_text       as flags_txt,
	sysmaster:syssessions      as session,
	outer sysmaster:sysrstcb   as wait_thread
	
where   tabname.partnum     = lcktab.partnum
	and txptab.address      = lcktab.owner
	and lock_thread.address = txptab.owner
	and lcktab.wtlist      	= wait_thread.address   -- lock waiters 
	and flags_txt.flags    	= lcktab.type
	and session.sid         = lock_thread.sid
	and flags_txt.tabname   = 'syslcktab'
	and tabname.dbsname    != 'sysmaster'   		-- real databases only
	and tabname.tabname    not like '% %'  		    -- real tables only
	and flags_txt.txt      not like '%I%'  		    -- ignore ""intended"" locks 
	and tabname.tabname <> 'command_history'
limit 50;
------------------------------------------------------------------------------------------------------------------