using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Diz.Core.serialization.xml_serializer
{
    // - Integrity checks run and version numbers are compatible for migrations
    // - any XML-based migrations (from ExtendedXmlSerializer etc) have already run
    public class MigrationRunner : IMigrationRunner
    {
        // constraints:
        // - every item in this list must have it's target version >= the previous version (sorted)
        // - multiple of the same version# ARE allowed
        // - items of equal version# will be applied in the order they're in the list
        // - caller must keep this sorted
        public List<IMigration> Migrations { get; init; } = new();

        IReadOnlyList<IMigration> IMigrationRunner.Migrations => Migrations;
        
        // when run, we'll filter out any migrations not between the Start and Target ranges
        public int StartingSaveVersion { get; set; }
        public int TargetSaveVersion { get; set; }

        public MigrationRunner(IReadOnlyList<IMigration> migrations) : this() 
        {
            if (migrations != null)
                Migrations = migrations.ToList();
        }

        public MigrationRunner()
        {
        }

        private void PreMigrationChecks()
        {
            var currentVersion = int.MinValue;
            foreach (var migration in Migrations)
            {
                if (migration == null)
                    throw new DizMigrationException(
                        "internal: all migrations must be non-null");

                if (migration.AppliesToSaveVersion < currentVersion)
                    throw new DizMigrationException(
                        "internal: all migrations must >= other migrations in the sequence");

                EnsureVersionIsSameOrNext(migration.AppliesToSaveVersion, currentVersion);

                currentVersion = migration.AppliesToSaveVersion;
            }
            
            if (StartingSaveVersion > TargetSaveVersion)
                throw new DizMigrationException(
                    "internal: starting migration version is greater than target version");
        }

        private void RunAllMigrations(Action<IMigration> applyAction)
        {
            PreMigrationChecks();
            
            if (StartingSaveVersion == TargetSaveVersion)
                return;
            
            int lastStartingVersionApplied = -1, lastStartingVersionConsidered = StartingSaveVersion;
            
            foreach(var migration in Migrations)
            {
                if (ValidateVersionForNext(migration.AppliesToSaveVersion, lastStartingVersionConsidered))
                {
                    applyAction(migration);
                    lastStartingVersionApplied = migration.AppliesToSaveVersion;
                }

                lastStartingVersionConsidered = migration.AppliesToSaveVersion;
            }

            FinalChecks(lastStartingVersionApplied + 1);
        }

        private void FinalChecks(int finalVersionReached)
        {
            if (finalVersionReached != TargetSaveVersion)
                throw new DizMigrationException(
                    $"migration failed. we were trying to upgrade to v{TargetSaveVersion}, but the final version# achieved was v{finalVersionReached}");
        }

        // throw an exception if anything is out of bounds
        // return true if we should apply this migration version
        private bool ValidateVersionForNext(int proposedVersion, int currentVersion)
        {
            if (proposedVersion < StartingSaveVersion)
                return false;
            
            if (!EnsureVersionIsSameOrNext(proposedVersion, currentVersion))
                throw new DizMigrationException(
                    $"internal: migration out of sequence. version {proposedVersion} not valid here. needed to upgrade from {currentVersion}");

            return proposedVersion >= StartingSaveVersion && proposedVersion < TargetSaveVersion;
        }

        // you can apply any mitigation that's at or 1 above our current version.
        // this is so multiple items of the same version number can be applied
        private static bool EnsureVersionIsSameOrNext(int proposedVersion, int currentVersion) => 
            proposedVersion == currentVersion || 
            proposedVersion == currentVersion + 1;

        public void OnLoadingBeforeAddLinkedRom(IAddRomDataCommand romAddCmd)
        {
            RunAllMigrations(migration => migration.OnLoadingBeforeAddLinkedRom(romAddCmd));
        }

        public void OnLoadingAfterAddLinkedRom(IAddRomDataCommand romAddCmd)
        {
            RunAllMigrations(migration => migration.OnLoadingAfterAddLinkedRom(romAddCmd));
        }
    }
}

public class DizMigrationException : Exception
{
    public DizMigrationException([CanBeNull] string message, [CanBeNull] Exception innerException) : base(message, innerException)
    {
    }

    public DizMigrationException([CanBeNull] string message) : base(message)
    {
    }
}