using java.sql;
using java.util;

namespace Ormix.Extensions
{
    public static class ResultSetDateFieldExtensions
    {
        public static DateTime? GetDateTimeFromDate(this java.sql.Date date)
        {
            if (date == null)
                return null;

            Calendar calendar = Calendar.getInstance();
            calendar.setTime(date);
            int year = calendar.get(Calendar.YEAR);
            int month = calendar.get(Calendar.MONTH) + 1;
            int day = calendar.get(Calendar.DAY_OF_MONTH);
            return new DateTime(year, month, day);
        }

        public static DateTime? GetDateTime(this ResultSet result, string columnLabel)
        {
            var date = result.getDate(columnLabel);
            return date.GetDateTimeFromDate();
        }

        public static DateTime? GetDateTime(this ResultSet result, int columnIndex, string columnLabel = "")
        {
            var date = result.getDate(columnIndex);
            return date.GetDateTimeFromDate();
        }


        [Obsolete()]
        public static Timestamp GetTimestampFromDateTime(this DateTime dateTime)
        {
            /*
             Timestamp timestamp = new Timestamp(year - 1900, month - 1, day, 0, 0, 0, 0); 
             neden year - 1900 ve  month - 1 yaptik ?
            ---------------------------------------------------------------------------
            Bu dönüşümler, java.sql.Timestamp ve java.util.Date sınıflarının 
            tarihi temsil etme şeklini dengelemek için yapılır. 
            Bu tür bir dönüşüm kökenini tarihsel olarak Java'nın tarih ve saat işleme şeklinden almaktadır.

            Yıl (year):
            java.sql.Timestamp sınıfı, yıl bilgisini 1900 yılından itibaren hesaplar. 
            Örneğin, 2023 yılı için java.sql.Timestamp sınıfına 123 değeri verilmelidir (2023 - 1900 = 123). 
            Bu, Java'nın tarih temsilinde yılların 1900'den başlayarak sayıldığı eski bir uygulamadan kaynaklanmaktadır.

            Ay (month):
            Ayların dizini 0'dan başlar (Ocak ayı 0, Şubat ayı 1, Mart ayı 2, ... Aralık ayı 11). 
            Bu nedenle, java.sql.Timestamp sınıfındaki ay parametresine geçirilen değer 1 eksiltme işlemine tabi tutulur. 
            Örneğin, Eylül ayı için 8 değeri (9 - 1 = 8) geçirilmelidir.

            Bu tür bir tarih temsilini kullanma alışkanlığı, java.util.Date 
            sınıfının deprecate edilmesine ve java.time paketinin kullanımının 
            teşvik edilmesine rağmen, geçmişteki tarih işleme uygulamalarından 
            kaynaklanan bir gelenektir. 
            
            Modern Java uygulamalarında, java.time.LocalDate veya java.time.LocalDateTime gibi 
            sınıflar genellikle tercih edilmektedir.            
            */

            return new Timestamp(dateTime.Year - 1900,
                                 dateTime.Month - 1,
                                 dateTime.Day,
                                 dateTime.Hour,
                                 dateTime.Minute,
                                 dateTime.Second,
                                 dateTime.Millisecond);
        }
    }

}
