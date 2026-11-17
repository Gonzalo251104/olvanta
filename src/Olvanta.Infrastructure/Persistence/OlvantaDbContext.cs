using Microsoft.EntityFrameworkCore;

namespace Olvanta.Infrastructure.Persistence;

public sealed class OlvantaDbContext(DbContextOptions<OlvantaDbContext> options) : DbContext(options);
