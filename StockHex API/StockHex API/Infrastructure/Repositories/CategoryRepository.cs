using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context) => _context = context;

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<PagedResult<Category>> GetPagedAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c => c.Name.Contains(term) ||
                                     (c.Description != null && c.Description.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(c => c.Products)
            .OrderBy(c => c.Name)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Category>(items, total, request.Page, request.PageSize);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        _context.Categories.AnyAsync(
            c => c.Name == name && (excludeId == null || c.Id != excludeId),
            cancellationToken);

    public Task<int> CountProductsAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _context.Products.CountAsync(p => p.CategoryId == categoryId, cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default) =>
        await _context.Categories.AddAsync(category, cancellationToken);

    public void Update(Category category) => _context.Categories.Update(category);

    public void Remove(Category category) => _context.Categories.Remove(category);
}
