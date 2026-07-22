using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocMgr.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // 基础数据表
        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Cabinet> Cabinets { get; set; }
        public DbSet<CabinetHardDiskSlotCategoryAssignment> CabinetHardDiskSlotCategoryAssignments { get; set; }
        public DbSet<CabinetArchiveSlotCategoryAssignment> CabinetArchiveSlotCategoryAssignments { get; set; }
        public DbSet<CabinetArchiveBoxPlacement> CabinetArchiveBoxPlacements { get; set; }
        public DbSet<CabinetSlotSpecification> CabinetSlotSpecifications { get; set; }
        public DbSet<ArchiveBoxSpecification> ArchiveBoxSpecifications { get; set; }
        public DbSet<CabinetSlotSpecialRule> CabinetSlotSpecialRules { get; set; }
        public DbSet<AerialPhoto> AerialPhotos { get; set; }
        public DbSet<OtherMap> OtherMaps { get; set; }
        public DbSet<TopoMap> TopoMaps { get; set; }
        public DbSet<ProjectInfo> ProjectInfos { get; set; }
        public DbSet<HardDiskMedium> HardDiskMedia { get; set; }
        public DbSet<HardDiskLedger> HardDiskLedgers { get; set; }
        public DbSet<HardDiskRegisterLock> HardDiskRegisterLocks { get; set; }
        public DbSet<HardDiskMediaApplication> HardDiskMediaApplications { get; set; }
        public DbSet<HardDiskMediaTransaction> HardDiskMediaTransactions { get; set; }
        public DbSet<HardDiskDisposalRecord> HardDiskDisposalRecords { get; set; }
        public DbSet<HardDiskDisposalItem> HardDiskDisposalItems { get; set; }

        // === 年度资料登记相关表 ===
        public DbSet<YearlyArchiveRegisterRecord> YearlyArchiveRegisterRecords { get; set; }
        public DbSet<YearlyArchiveRegisterMedia> YearlyArchiveRegisterMedias { get; set; }
        public DbSet<YearlyArchiveRegisterMediaItem> YearlyArchiveRegisterMediaItems { get; set; }
        public DbSet<YearlyArchiveRegisterElectronicMediaItemDetail> YearlyArchiveRegisterElectronicMediaItemDetails { get; set; }
        public DbSet<YearlyArchiveRegisterElectronicMediaItemEntry> YearlyArchiveRegisterElectronicMediaItemEntries { get; set; }

        // 年度资料档案盒
        public DbSet<YearlyArchiveBox> YearlyArchiveBoxes { get; set; }
        public DbSet<YearlyArchiveBoxMediaItemLink> YearlyArchiveBoxMediaItemLinks { get; set; }
        public DbSet<YearlyElectronicArchiveUnit> YearlyElectronicArchiveUnits { get; set; }
        public DbSet<YearlyElectronicArchiveUnitMediumLink> YearlyElectronicArchiveUnitMediumLinks { get; set; }
        public DbSet<OpticalDiscMedium> OpticalDiscMedia { get; set; }
        public DbSet<OpticalDiscLedger> OpticalDiscLedgers { get; set; }
        public DbSet<OpticalDiscMediaTransaction> OpticalDiscMediaTransactions { get; set; }
        public DbSet<YearlyElectronicArchiveUnitDiscLink> YearlyElectronicArchiveUnitDiscLinks { get; set; }
        public DbSet<YearlyElectronicArchiveUnitMediaLink> YearlyElectronicArchiveUnitMediaLinks { get; set; }
        public DbSet<YearlyElectronicArchiveUnitMediaItemLink> YearlyElectronicArchiveUnitMediaItemLinks { get; set; }
        public DbSet<YearlyArchiveFilingFact> YearlyArchiveFilingFacts { get; set; }
        public DbSet<YearlyArchiveSearchResultSet> YearlyArchiveSearchResultSets { get; set; }
        public DbSet<YearlyArchiveSearchResultSetItem> YearlyArchiveSearchResultSetItems { get; set; }
        public DbSet<YearlyArchiveRelocationRecord> YearlyArchiveRelocationRecords { get; set; }
        public DbSet<YearlyArchiveRelocationItem> YearlyArchiveRelocationItems { get; set; }
        public DbSet<YearlyArchiveOutboundRecord> YearlyArchiveOutboundRecords { get; set; }
        public DbSet<YearlyArchiveOutboundItem> YearlyArchiveOutboundItems { get; set; }
        public DbSet<YearlyArchiveOutboundSyncEntry> YearlyArchiveOutboundSyncEntries { get; set; }
        public DbSet<YearlyArchiveReturnRecord> YearlyArchiveReturnRecords { get; set; }
        public DbSet<YearlyArchiveReturnItem> YearlyArchiveReturnItems { get; set; }
        public DbSet<YearlyArchiveMaterialTransaction> YearlyArchiveMaterialTransactions { get; set; }

        // === 通用附件表 ===
        public DbSet<SystemAttachment> SystemAttachments { get; set; }

        // 待办事项相关表
        public DbSet<ToDoReadState> ToDoReadStates { get; set; }

        // 扩展配置表
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<BusinessLogicSettings> BusinessLogicSettings { get; set; }
        public DbSet<FieldDomainDefinition> FieldDomainDefinitions { get; set; }
        public DbSet<FieldDomainOption> FieldDomainOptions { get; set; }

        public DbSet<DbOperationLog> DbOperationLogs { get; set; }

        public DbSet<ArchiveContainerProjection> ArchiveContainerSummaries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            modelBuilder.Entity<ArchiveContainerProjection>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_ArchiveContainerSummaries");
            });

            modelBuilder.Entity<CabinetArchiveBoxPlacement>(entity =>
            {
                entity.HasIndex(item => item.BoxCode)
                    .IsUnique();

                entity.HasIndex(item => new { item.CabinetName, item.FaceCode, item.SlotCode });

                entity.HasIndex(item => new { item.SourceType, item.SourceRecordKey });
            });

            modelBuilder.Entity<CabinetHardDiskSlotCategoryAssignment>(entity =>
            {
                entity.HasIndex(item => new { item.CabinetId, item.FaceCode, item.SlotCode })
                    .IsUnique();

                entity.HasIndex(item => new { item.CabinetId, item.CategoryName });

                entity.HasOne(item => item.Cabinet)
                    .WithMany()
                    .HasForeignKey(item => item.CabinetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CabinetArchiveSlotCategoryAssignment>(entity =>
            {
                entity.HasIndex(item => new { item.CabinetId, item.FaceCode, item.SlotCode })
                    .IsUnique();

                entity.HasIndex(item => new { item.CabinetId, item.CategoryName });

                entity.HasOne(item => item.Cabinet)
                    .WithMany()
                    .HasForeignKey(item => item.CabinetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<HardDiskMedium>(entity =>
            {
                entity.HasIndex(item => item.DiskCode)
                    .IsUnique();

                entity.HasIndex(item => item.SerialNumber);

                entity.HasMany(item => item.Applications)
                    .WithOne(application => application.Medium)
                    .HasForeignKey(application => application.MediumId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(item => item.Transactions)
                    .WithOne(transaction => transaction.Medium)
                    .HasForeignKey(transaction => transaction.MediumId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(item => item.ElectronicArchiveLinks)
                    .WithOne(link => link.HardDiskMedium)
                    .HasForeignKey(link => link.HardDiskMediumId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.Ledger)
                    .WithOne(ledger => ledger.Medium)
                    .HasForeignKey<HardDiskLedger>(ledger => ledger.MediumId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.RegisterLock)
                    .WithOne(lockItem => lockItem.Medium)
                    .HasForeignKey<HardDiskRegisterLock>(lockItem => lockItem.MediumId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<HardDiskLedger>(entity =>
            {
                entity.HasIndex(item => item.MediumId)
                    .IsUnique();

                entity.HasIndex(item => item.DiskCode)
                    .IsUnique();
            });

            modelBuilder.Entity<HardDiskRegisterLock>(entity =>
            {
                entity.HasIndex(item => item.MediumId)
                    .IsUnique();

                entity.HasIndex(item => new { item.BusinessType, item.BusinessRecordId });

                entity.HasIndex(item => item.BusinessNo);
            });

            modelBuilder.Entity<OpticalDiscMedium>(entity =>
            {
                entity.HasIndex(item => item.DiscCode)
                    .IsUnique();

                entity.HasMany(item => item.ElectronicArchiveLinks)
                    .WithOne(link => link.OpticalDiscMedium)
                    .HasForeignKey(link => link.OpticalDiscMediumId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.Ledger)
                    .WithOne(ledger => ledger.Medium)
                    .HasForeignKey<OpticalDiscLedger>(ledger => ledger.MediumId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(item => item.Transactions)
                    .WithOne(transaction => transaction.Medium)
                    .HasForeignKey(transaction => transaction.MediumId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OpticalDiscLedger>(entity =>
            {
                entity.HasIndex(item => item.MediumId)
                    .IsUnique();

                entity.HasIndex(item => item.DiscCode)
                    .IsUnique();
            });

            modelBuilder.Entity<OpticalDiscMediaTransaction>(entity =>
            {
                entity.HasIndex(item => new { item.MediumId, item.OperateTime });
            });

            modelBuilder.Entity<HardDiskMediaApplication>(entity =>
            {
                entity.HasIndex(item => item.ApplicationNo)
                    .IsUnique();

                entity.HasIndex(item => item.SourceApplicationId);

                entity.HasIndex(item => item.SourceOutboundRecordId);

                entity.HasOne(item => item.Medium)
                    .WithMany(medium => medium.Applications)
                    .HasForeignKey(item => item.MediumId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<HardDiskMediaTransaction>(entity =>
            {
                entity.HasIndex(item => new { item.MediumId, item.OperateTime });

                entity.HasOne(item => item.Medium)
                    .WithMany(medium => medium.Transactions)
                    .HasForeignKey(transaction => transaction.MediumId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.Application)
                    .WithMany()
                    .HasForeignKey(item => item.ApplicationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<HardDiskDisposalRecord>(entity =>
            {
                entity.HasIndex(item => item.DisposalNo).IsUnique();
                entity.HasIndex(item => item.Status);
                entity.HasIndex(item => item.ApplyTime);

                entity.HasMany(item => item.Items)
                    .WithOne(item => item.DisposalRecord)
                    .HasForeignKey(item => item.DisposalRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<HardDiskDisposalItem>(entity =>
            {
                entity.HasIndex(item => item.DisposalRecordId);
                entity.HasIndex(item => item.MediumId);
                entity.HasIndex(item => new { item.DisposalRecordId, item.MediumId }).IsUnique();

                entity.HasOne(item => item.Medium)
                    .WithMany()
                    .HasForeignKey(item => item.MediumId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<YearlyElectronicArchiveUnit>(entity =>
            {
                entity.HasIndex(item => item.ElectronicArchiveNo)
                    .IsUnique();

                entity.HasMany(item => item.MediumLinks)
                    .WithOne(link => link.ElectronicArchiveUnit)
                    .HasForeignKey(link => link.YearlyElectronicArchiveUnitId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(item => item.MediaEntryLinks)
                    .WithOne(link => link.ElectronicArchiveUnit)
                    .HasForeignKey(link => link.YearlyElectronicArchiveUnitId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(item => item.DiscLinks)
                    .WithOne(link => link.ElectronicArchiveUnit)
                    .HasForeignKey(link => link.YearlyElectronicArchiveUnitId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<YearlyArchiveBoxMediaItemLink>(entity =>
            {
                entity.HasIndex(item => new { item.YearlyArchiveBoxId, item.YearlyArchiveRegisterMediaItemId })
                    .IsUnique();

                entity.HasIndex(item => item.YearlyArchiveRegisterMediaItemId)
                    .IsUnique();

                entity.HasOne(item => item.ArchiveBox)
                    .WithMany(box => box.MediaItemLinks)
                    .HasForeignKey(item => item.YearlyArchiveBoxId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.MediaItem)
                    .WithMany(mediaItem => mediaItem.ArchiveBoxLinks)
                    .HasForeignKey(item => item.YearlyArchiveRegisterMediaItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<YearlyElectronicArchiveUnitMediumLink>(entity =>
            {
                entity.HasIndex(item => new { item.YearlyElectronicArchiveUnitId, item.HardDiskMediumId })
                    .IsUnique();
            });

            modelBuilder.Entity<YearlyElectronicArchiveUnitDiscLink>(entity =>
            {
                entity.HasIndex(item => new { item.YearlyElectronicArchiveUnitId, item.OpticalDiscMediumId })
                    .IsUnique();
            });

            modelBuilder.Entity<YearlyElectronicArchiveUnitMediaLink>(entity =>
            {
                entity.HasIndex(item => new { item.YearlyElectronicArchiveUnitId, item.YearlyArchiveRegisterMediaId })
                    .IsUnique();

                entity.HasIndex(item => item.YearlyArchiveRegisterMediaId)
                    .IsUnique();

                entity.HasOne(item => item.MediaEntry)
                    .WithMany(media => media.ElectronicArchiveUnitLinks)
                    .HasForeignKey(item => item.YearlyArchiveRegisterMediaId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<YearlyElectronicArchiveUnitMediaItemLink>(entity =>
            {
                entity.HasIndex(item => new { item.YearlyElectronicArchiveUnitId, item.YearlyArchiveRegisterMediaItemId })
                    .IsUnique();

                entity.HasIndex(item => item.YearlyArchiveRegisterMediaItemId)
                    .IsUnique();

                entity.HasOne(item => item.ElectronicArchiveUnit)
                    .WithMany(unit => unit.MediaItemLinks)
                    .HasForeignKey(item => item.YearlyElectronicArchiveUnitId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.MediaItem)
                    .WithMany(mediaItem => mediaItem.ElectronicArchiveUnitMediaItemLinks)
                    .HasForeignKey(item => item.YearlyArchiveRegisterMediaItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<YearlyArchiveRegisterMedia>(entity =>
            {
                entity.HasOne(media => media.RegisterRecord)
                    .WithMany(record => record.MediaEntries)
                    .HasForeignKey(media => media.YearlyArchiveRegisterRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<YearlyArchiveRegisterMediaItem>(entity =>
            {
                entity.HasOne(item => item.MediaEntry)
                    .WithMany(media => media.Items)
                    .HasForeignKey(item => item.YearlyArchiveRegisterMediaId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DbOperationLog>(entity =>
            {
                entity.HasIndex(item => item.OperationTime);
                entity.HasIndex(item => item.TableName);
                entity.HasIndex(item => item.EntityType);
                entity.HasIndex(item => item.Operation);
            });
        }

        public override int SaveChanges()
        {
            NormalizeCabinetNames();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            NormalizeCabinetNames();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            NormalizeCabinetNames();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            NormalizeCabinetNames();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void NormalizeCabinetNames()
        {
            foreach (var entry in ChangeTracker.Entries<Cabinet>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Entity.Name = CabinetNameNormalizer.Normalize(entry.Entity.Name);
                }
            }

            foreach (var entry in ChangeTracker.Entries<CabinetSlotSpecialRule>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Entity.CabinetName = CabinetNameNormalizer.Normalize(entry.Entity.CabinetName);
                }
            }

            foreach (var entry in ChangeTracker.Entries<CabinetArchiveBoxPlacement>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Entity.CabinetName = CabinetNameNormalizer.Normalize(entry.Entity.CabinetName);
                }
            }

            foreach (var entry in ChangeTracker.Entries<YearlyArchiveBox>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Entity.CabinetName = CabinetNameNormalizer.Normalize(entry.Entity.CabinetName);
                }
            }

            foreach (var entry in ChangeTracker.Entries<HardDiskMedium>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    if (entry.Entity.Ledger != null)
                    {
                        entry.Entity.Ledger.MediaStatus = NormalizeMediumStatusText(entry.Entity.Ledger.MediaStatus);
                    }
                }
            }

            foreach (var entry in ChangeTracker.Entries<OpticalDiscLedger>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Entity.MediaStatus = NormalizeMediumStatusText(entry.Entity.MediaStatus);
                }
            }
        }

        private static string NormalizeMediumStatusText(string? statusText)
            => MediumStatusTextNormalizer.Normalize(statusText);

    }
}