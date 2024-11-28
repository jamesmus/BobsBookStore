using Bookstore.Domain;
using Bookstore.Domain.Books;
using Bookstore.Domain.ReferenceData;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Bookstore.Data.Repositories
{
    public class PaginatedList<T> : List<T>, IPaginatedList<T>
    {
        public int PageIndex { get; private set; }
        public int TotalPages { get; private set; }
        public int TotalCount { get; private set; }
        private readonly IQueryable<T> _source;
        private readonly int _pageSize;

        public PaginatedList(IQueryable<T> source, int pageIndex, int pageSize)
        {
            _source = source;
            _pageSize = pageSize;
            PageIndex = pageIndex;
            TotalCount = source.Count();
            TotalPages = (int)Math.Ceiling(TotalCount / (double)pageSize);

            this.AddRange(source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList());
        }

        public bool HasPreviousPage => PageIndex > 1;

        public bool HasNextPage => PageIndex < TotalPages;

        public Task PopulateAsync()
        {
            return Task.CompletedTask;
        }

        public IEnumerable<int> GetPageList(int pageCount)
        {
            int startPage = Math.Max(1, PageIndex - (pageCount / 2));
            int endPage = Math.Min(TotalPages, startPage + pageCount - 1);

            return Enumerable.Range(startPage, endPage - startPage + 1);
        }
    }

    public class ReferenceDataRepository : IReferenceDataRepository
    {
        private readonly ApplicationDbContext dbContext;

        public ReferenceDataRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        async Task IReferenceDataRepository.AddAsync(ReferenceDataItem item)
        {
            await Task.Run(() => dbContext.ReferenceData.Add(item));
        }

        async Task<ReferenceDataItem> IReferenceDataRepository.GetAsync(int id)
        {
            return await dbContext.ReferenceData.FindAsync(id);
        }

        async Task<IEnumerable<ReferenceDataItem>> IReferenceDataRepository.FullListAsync()
        {
            return await dbContext.ReferenceData.ToListAsync();
        }

        async Task<IPaginatedList<ReferenceDataItem>> IReferenceDataRepository.ListAsync(ReferenceDataFilters filters, int pageIndex, int pageSize)
        {
            var query = dbContext.ReferenceData.AsQueryable();

            if (filters.ReferenceDataType.HasValue)
            {
                query = query.Where(x => x.DataType == filters.ReferenceDataType.Value);
            }

            var result = new PaginatedList<ReferenceDataItem>(query, pageIndex, pageSize);

            return result;
        }

        async Task IReferenceDataRepository.SaveChangesAsync()
        {
            await dbContext.SaveChangesAsync();
        }
    }
}