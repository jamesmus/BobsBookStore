using Bookstore.Domain;
using Bookstore.Domain.Books;
using System;
using System.Collections;
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

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            TotalCount = count;

            this.AddRange(items);
        }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
        {
            var count = await source.CountAsync();
            var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }

        public Task PopulateAsync()
        {
            // This method is already populated in the constructor, so we can just return a completed task
            return Task.CompletedTask;
        }

        public IEnumerable<int> GetPageList(int pageSize)
        {
            var pageCount = (int)Math.Ceiling(TotalCount / (double)pageSize);
            return Enumerable.Range(1, pageCount);
        }
    }

    public class BookRepository : IBookRepository
    {
        private readonly ApplicationDbContext dbContext;

        public BookRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        async Task<Book> IBookRepository.GetAsync(int id)
        {
            return await dbContext.Book
                .Include("Genre")
                .Include("Publisher")
                .Include("BookType")
                .Include("Condition")
                .SingleAsync(x => x.Id == id);
        }

        async Task<IPaginatedList<Book>> IBookRepository.ListAsync(BookFilters filters, int pageIndex, int pageSize)
        {
            var query = dbContext.Book.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filters.Name))
            {
                query = query.Where(x => x.Name.Contains(filters.Name));
            }

            if (!string.IsNullOrWhiteSpace(filters.Author))
            {
                query = query.Where(x => x.Author.Contains(filters.Author));
            }

            if (filters.ConditionId.HasValue)
            {
                query = query.Where(x => x.ConditionId == filters.ConditionId);
            }

            if (filters.BookTypeId.HasValue)
            {
                query = query.Where(x => x.BookTypeId == filters.BookTypeId);
            }

            if (filters.GenreId.HasValue)
            {
                query = query.Where(x => x.GenreId == filters.GenreId);
            }

            if (filters.PublisherId.HasValue)
            {
                query = query.Where(x => x.PublisherId == filters.PublisherId);
            }

            if (filters.LowStock)
            {
                query = query.Where(x => x.Quantity <= Book.LowBookThreshold);
            }

            query = query
                .Include(x => x.Genre)
                .Include(x => x.Publisher)
                .Include(x => x.BookType)
                .Include(x => x.Condition);

            return await PaginatedList<Book>.CreateAsync(query, pageIndex, pageSize);
        }

        async Task<IPaginatedList<Book>> IBookRepository.ListAsync(string searchString, string sortBy, int pageIndex, int pageSize)
        {
            var query = dbContext.Book.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(x => x.Name.Contains(searchString) ||
                                         x.Genre.Text.Contains(searchString) ||
                                         x.BookType.Text.Contains(searchString) ||
                                         x.ISBN.Contains(searchString) ||
                                         x.Publisher.Text.Contains(searchString));
            };

            switch (sortBy)
            {
                case "Name":
                    query = query.OrderBy(x => x.Name);
                    break;

                case "PriceAsc":
                    query = query.OrderBy(x => x.Price);
                    break;

                case "PriceDesc":
                    query = query.OrderByDescending(x => x.Price);
                    break;

                default:
                    query = query.OrderBy(x => x.Name);
                    break;
            }

            return await PaginatedList<Book>.CreateAsync(query, pageIndex, pageSize);
        }

        async Task IBookRepository.AddAsync(Book book)
        {
            await Task.Run(() => dbContext.Book.Add(book));
        }

        async Task IBookRepository.UpdateAsync(Book book)
        {
            var existing = await dbContext.Book.FindAsync(book.Id);

            dbContext.Entry(existing).CurrentValues.SetValues(book);

            if (string.IsNullOrWhiteSpace(book.CoverImageUrl))
            {
                dbContext.Entry(existing).Property(x => x.CoverImageUrl).IsModified = false;
            }
        }

        async Task IBookRepository.SaveChangesAsync()
        {
            await dbContext.SaveChangesAsync();
        }

        async Task<BookStatistics> IBookRepository.GetStatisticsAsync()
        {
            return await dbContext.Book
                .GroupBy(x => 1)
                .Select(x => new BookStatistics
                {
                    LowStock = x.Count(y => y.Quantity > 0 && y.Quantity < Book.LowBookThreshold),
                    OutOfStock = x.Count(y => y.Quantity == 0),
                    StockTotal = x.Count()
                }).SingleOrDefaultAsync();
        }
    }
}