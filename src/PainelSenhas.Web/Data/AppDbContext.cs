using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PainelSenhas.Web.Data;

/// <summary>
/// Contexto único do sistema: dados de atendimento e tabelas do ASP.NET Identity. Este tipo é o contexto do SQLite;
/// o SQL Server usa <see cref="SqlServerDbContext"/>, que herda daqui o mesmo modelo.
/// </summary>
internal class AppDbContext : IdentityUserContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected AppDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Desk> Desks => Set<Desk>();

    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<DailySequence> Sequences => Set<DailySequence>();
    public DbSet<TicketEvent> Events => Set<TicketEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<Department>().HasIndex(x => x.Prefix).IsUnique();
        model.Entity<Department>().Property(x => x.Name).HasMaxLength(80);
        model.Entity<Department>().Property(x => x.Prefix).HasMaxLength(5);
        model.Entity<Desk>().HasIndex(x => x.DepartmentId).IsUnique();
        model.Entity<Desk>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<UserDepartment>().HasKey(x => new { x.UserId, x.DepartmentId });
        model.Entity<UserDepartment>().HasIndex(x => x.DepartmentId);
        model.Entity<UserDepartment>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<UserDepartment>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Ticket>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Ticket>().HasOne<Desk>().WithMany().HasForeignKey(x => x.DeskId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Ticket>().HasIndex(x => new { x.DepartmentId, x.BusinessDate, x.Number }).IsUnique();
        model.Entity<Ticket>().HasIndex(x => x.IssueKey).IsUnique();
        model.Entity<Ticket>().HasIndex(x => new { x.DepartmentId, x.Status, x.IsPreferential, x.BusinessDate, x.Number });
        model.Entity<Ticket>().HasIndex(x => x.DeskId).IsUnique()
            .HasFilter(Database.IsSqlServer() ? "[Status] = 1" : "\"Status\" = 1");
        model.Entity<DailySequence>().HasKey(x => new { x.DepartmentId, x.BusinessDate });
        model.Entity<DailySequence>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<TicketEvent>().HasIndex(x => x.OperationKey).IsUnique();
        model.Entity<TicketEvent>().HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<TicketEvent>().HasOne<Desk>().WithMany().HasForeignKey(x => x.DeskId).OnDelete(DeleteBehavior.Restrict);
    }
}
