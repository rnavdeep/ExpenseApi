using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Expense.API.IntegrationTests.Infrastructure;

/// <summary>
/// The business EF model has several overlapping cascade-delete paths (Documents, DocumentJobResults,
/// ExpenseUsers, Notifications and FriendRequests all cascade toward Users/Expenses), which SQL Server
/// rejects when it materialises the schema ("may cause cycles or multiple cascade paths"). Production
/// never hits this because the real database is provisioned from hand-written DDL, but our test host
/// creates the schema from the model via EnsureCreated. This customizer downgrades every foreign key
/// to a non-cascading delete so the schema can be created; tests never rely on DB-side cascade deletes.
/// </summary>
public class RestrictCascadeModelCustomizer : RelationalModelCustomizer
{
    public RestrictCascadeModelCustomizer(ModelCustomizerDependencies dependencies) : base(dependencies) { }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
