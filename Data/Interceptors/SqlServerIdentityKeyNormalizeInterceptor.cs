using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocMgr.Data.Interceptors;

/// <summary>
/// SQL Server 下拦截误设的 IDENTITY 主键：新增实体若客户端写入了非 0 的 int/long 自增主键，
/// 在 SaveChanges 前清零并标为临时键，交由数据库生成，避免「IDENTITY_INSERT 设置为 OFF」异常。
/// <para>
/// 注意：EF Core 会为 Added 实体分配临时主键（常为负数，且 <see cref="PropertyEntry.IsTemporary"/> 为 true）。
/// 这些临时键绝不可清零，否则多个新增实体会同时变成 Id=0 并触发同键跟踪冲突。
/// </para>
/// </summary>
public sealed class SqlServerIdentityKeyNormalizeInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Normalize(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Normalize(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Normalize(DbContext? context)
    {
        if (context == null || !context.Database.IsSqlServer())
        {
            return;
        }

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            foreach (PropertyEntry property in entry.Properties)
            {
                IProperty metadata = property.Metadata;
                if (!metadata.IsPrimaryKey()
                    || metadata.ValueGenerated != ValueGenerated.OnAdd
                    || metadata.IsForeignKey())
                {
                    continue;
                }

                // EF 已分配的临时主键必须保留，否则多实体会撞 Id=0。
                if (property.IsTemporary)
                {
                    continue;
                }

                if (metadata.ClrType == typeof(int)
                    && property.CurrentValue is int intId
                    && intId != 0)
                {
                    // 客户端误带的正数主键：清零并重新标为临时键，交给库生成。
                    property.CurrentValue = 0;
                    property.IsTemporary = true;
                }
                else if (metadata.ClrType == typeof(long)
                         && property.CurrentValue is long longId
                         && longId != 0)
                {
                    property.CurrentValue = 0L;
                    property.IsTemporary = true;
                }
            }
        }
    }
}
