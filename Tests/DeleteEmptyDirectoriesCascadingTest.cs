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
