using ImageSorter;
using Xunit;

namespace ImageSorterTests
{
    public class DeleteEmptyDirectoriesCascadingTest
    {
        private static string CreateTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "ImageSorterTests-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            return root;
        }

        [Fact]
        public void EmptyLeafFolder_IsDeleted()
        {
            var root = CreateTempRoot();
            var leaf = Path.Combine(root, "2020", "01");
            Directory.CreateDirectory(leaf);

            try
            {
                Util.DeleteEmptyDirectoriesCascading(new[] { leaf }, root, whatIf: false);

                Assert.False(Directory.Exists(leaf));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void NonEmptyFolder_IsNotDeleted()
        {
            var root = CreateTempRoot();
            var leaf = Path.Combine(root, "2020", "01");
            Directory.CreateDirectory(leaf);
            File.WriteAllText(Path.Combine(leaf, "remaining.txt"), "still here");

            try
            {
                Util.DeleteEmptyDirectoriesCascading(new[] { leaf }, root, whatIf: false);

                Assert.True(Directory.Exists(leaf));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void BothSiblingsEmpty_CascadesUpToParent()
        {
            var root = CreateTempRoot();
            var yearDir = Path.Combine(root, "2020");
            var january = Path.Combine(yearDir, "01");
            var february = Path.Combine(yearDir, "02");
            Directory.CreateDirectory(january);
            Directory.CreateDirectory(february);

            try
            {
                Util.DeleteEmptyDirectoriesCascading(new[] { january, february }, root, whatIf: false);

                Assert.False(Directory.Exists(january));
                Assert.False(Directory.Exists(february));
                Assert.False(Directory.Exists(yearDir));
                Assert.True(Directory.Exists(root));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void OneSiblingStillHasFile_ParentIsNotDeleted()
        {
            var root = CreateTempRoot();
            var yearDir = Path.Combine(root, "2020");
            var january = Path.Combine(yearDir, "01");
            var february = Path.Combine(yearDir, "02");
            Directory.CreateDirectory(january);
            Directory.CreateDirectory(february);
            File.WriteAllText(Path.Combine(february, "remaining.jpg"), "still here");

            try
            {
                Util.DeleteEmptyDirectoriesCascading(new[] { january, february }, root, whatIf: false);

                Assert.False(Directory.Exists(january));
                Assert.True(Directory.Exists(february));
                Assert.True(Directory.Exists(yearDir));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void SourceDirItself_IsNeverDeleted()
        {
            var root = CreateTempRoot();
            var leaf = Path.Combine(root, "2020");
            Directory.CreateDirectory(leaf);

            try
            {
                Util.DeleteEmptyDirectoriesCascading(new[] { leaf }, root, whatIf: false);

                Assert.False(Directory.Exists(leaf));
                Assert.True(Directory.Exists(root));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void DeepDayLevelStructure_AllEmpty_CascadesAllTheWayToRoot()
        {
            var root = CreateTempRoot();
            var yearDir = Path.Combine(root, "2026");
            var monthDir = Path.Combine(yearDir, "08");
            var day23 = Path.Combine(monthDir, "23");
            var day24 = Path.Combine(monthDir, "24");
            Directory.CreateDirectory(day23);
            Directory.CreateDirectory(day24);

            try
            {
                Util.DeleteEmptyDirectoriesCascading(new[] { day23, day24 }, root, whatIf: false);

                Assert.False(Directory.Exists(day23));
                Assert.False(Directory.Exists(day24));
                Assert.False(Directory.Exists(monthDir));
                Assert.False(Directory.Exists(yearDir));
                Assert.True(Directory.Exists(root));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void DeepDayLevelStructure_OneDayHasFile_StopsCascadeAtMonth()
        {
            var root = CreateTempRoot();
            var yearDir = Path.Combine(root, "2026");
            var monthDir = Path.Combine(yearDir, "08");
            var day23 = Path.Combine(monthDir, "23");
            var day24 = Path.Combine(monthDir, "24");
            Directory.CreateDirectory(day23);
            Directory.CreateDirectory(day24);
            File.WriteAllText(Path.Combine(day24, "remaining.jpg"), "still here");

            try
            {
                Util.DeleteEmptyDirectoriesCascading(new[] { day23, day24 }, root, whatIf: false);

                Assert.False(Directory.Exists(day23));
                Assert.True(Directory.Exists(day24));
                Assert.True(Directory.Exists(monthDir));
                Assert.True(Directory.Exists(yearDir));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void MultipleDateBranches_OnlyFullyEmptyBranchCascades()
        {
            var root = CreateTempRoot();

            var emptyYear = Path.Combine(root, "2026");
            var emptyMonth = Path.Combine(emptyYear, "08");
            var emptyDay1 = Path.Combine(emptyMonth, "23");
            var emptyDay2 = Path.Combine(emptyMonth, "24");
            Directory.CreateDirectory(emptyDay1);
            Directory.CreateDirectory(emptyDay2);

            var otherYear = Path.Combine(root, "2025");
            var otherMonth = Path.Combine(otherYear, "01");
            var otherDay = Path.Combine(otherMonth, "01");
            Directory.CreateDirectory(otherDay);
            File.WriteAllText(Path.Combine(otherDay, "untouched.jpg"), "not part of this run");

            try
            {
                // Only the 2026 branch's day folders were actually touched by this run -
                // 2025/01/01 was never a moved file's parent, so it isn't a starting candidate.
                Util.DeleteEmptyDirectoriesCascading(new[] { emptyDay1, emptyDay2 }, root, whatIf: false);

                Assert.False(Directory.Exists(emptyDay1));
                Assert.False(Directory.Exists(emptyDay2));
                Assert.False(Directory.Exists(emptyMonth));
                Assert.False(Directory.Exists(emptyYear));

                Assert.True(Directory.Exists(otherDay));
                Assert.True(Directory.Exists(otherMonth));
                Assert.True(Directory.Exists(otherYear));
                Assert.True(Directory.Exists(root));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void WhatIf_CascadesDecisionsButDeletesNothing()
        {
            var root = CreateTempRoot();
            var yearDir = Path.Combine(root, "2020");
            var january = Path.Combine(yearDir, "01");
            var february = Path.Combine(yearDir, "02");
            Directory.CreateDirectory(january);
            Directory.CreateDirectory(february);

            try
            {
                Util.DeleteEmptyDirectoriesCascading(new[] { january, february }, root, whatIf: true);

                Assert.True(Directory.Exists(january));
                Assert.True(Directory.Exists(february));
                Assert.True(Directory.Exists(yearDir));
                Assert.True(Directory.Exists(root));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
