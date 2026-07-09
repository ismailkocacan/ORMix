using System.Diagnostics;

namespace Ormix.Extensions
{
    public class ResultMetaData
    {
        public string ColumnName { get; set; }
        public int ColumnType { get; set; }
        public string ColumnTypeName { get; set; }
        public int ColumnSize { get; set; }
        public int ColumnPrecision { get; set; }
        public int ColumnScale { get; set; }
    }

    public class Result<T>
    {
        public Result()
        {
            this.Data = new List<T>();
        }

        public List<ResultMetaData> MetaData { get; set; }
        public List<T> Data { get; set; }

        public long RecordCount => Data.Count;

        public TimeSpan Elapsed { get; set; }

        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public void Start()
        {
            stopwatch.Reset();
            stopwatch.Start();
        }

        public void Stop()
        {
            stopwatch.Stop();
            this.Elapsed = stopwatch.Elapsed;
        }

        public static Result<T> CreateEmpty()
        {
            return new Result<T>()
            {
                MetaData = new List<ResultMetaData>()
            };
        }
    }

    public class AggregateResult<T, R> : Result<T>
    {
        public R AggregateValue { get; set; }
    }

    public class DynamicResult : Result<dynamic>
    {
        public static DynamicResult Empty()
        {
            return new DynamicResult()
            {
                MetaData = new List<ResultMetaData>()
            };
        }
    }

    public class DynamicCompiledResult : Result<object>
    {
        public static DynamicCompiledResult Empty()
        {
            return new DynamicCompiledResult()
            {
                MetaData = new List<ResultMetaData>()
            };
        }
    }


    public class Page
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public Page()
        {
            PageNumber = 1;
            PageSize = 20;
        }

        public Page(int pageNumber, int pageSize)
        {
            PageNumber = pageNumber;
            PageSize = pageSize;
        }

        public int Skip => PageSize * (PageNumber - 1);
    }

    public class PagingResult<T> : Result<T>
    {
        public Page Page { get; }

        public int TotalPages { get; }

        public int TotalRecordCount { get; }

        public bool HasPrevious
        {
            get => Page.PageNumber > 1;
        }

        public bool HasNext
        {
            get => Page.PageNumber < TotalPages;
        }

        public PagingResult(List<T> data, Page page, int totalRecordCount)
        {
            this.Data = data;
            this.Page = page;
            this.TotalRecordCount = totalRecordCount;
            this.TotalPages = (int)Math.Ceiling(totalRecordCount / (double)page.PageSize);
        }

        public static PagingResult<T> Empty(Page page, int totalRecordCount)
        {
            return new PagingResult<T>(new List<T>(), page, totalRecordCount);
        }
    }

    public class DynamicPagingResult : PagingResult<dynamic>
    {
        public DynamicPagingResult(List<dynamic> data, Page page, int totalRecordCount)
            : base(data, page, totalRecordCount)
        {

        }

        public static DynamicPagingResult Empty(Page page, int totalRecordCount)
        {
            return new DynamicPagingResult(new List<dynamic>(), page, totalRecordCount);
        }
    }
}
