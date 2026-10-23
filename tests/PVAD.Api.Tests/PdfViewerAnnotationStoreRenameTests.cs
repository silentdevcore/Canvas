using System.Text.Json;
using PVAD.WebApi.Services;

namespace PVAD.Api.Tests;

public sealed class PdfViewerAnnotationStoreRenameTests
{
    [Fact]
    public void Restart_preserves_existing_annotation_file_names_and_contents_without_rebranding()
    {
        var root = Directory.CreateTempSubdirectory("pvad-annotation-rename-").FullName;
        try
        {
            var mount = Path.Combine(root, "retained-pxa-mount");
            using var payload = JsonDocument.Parse("[{\"id\":\"note-1\",\"text\":\"Synthetic PXA source text\"}]");
            var original = new PdfViewerAnnotationStore(mount).Save("synthetic-pxa-document", 1,
                "Synthetic PXA source.pdf", DateTimeOffset.Parse("2026-10-21T00:00:00Z"), payload.RootElement);
            var path = Assert.Single(Directory.GetFiles(mount));
            var bytes = File.ReadAllBytes(path);
            var restarted = new PdfViewerAnnotationStore(mount);
            Assert.True(restarted.TryGet(original.DocumentId, out var restored));
            Assert.Equal(original.DocumentId, restored.DocumentId);
            Assert.Equal(original.SourceName, restored.SourceName);
            Assert.Equal(original.Version, restored.Version);
            Assert.Equal(original.ExportedAt, restored.ExportedAt);
            Assert.Equal(original.SavedAt, restored.SavedAt);
            Assert.True(JsonElement.DeepEquals(original.Annotations, restored.Annotations));
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(path, Assert.Single(Directory.GetFiles(mount)));
            Assert.False(new PdfViewerAnnotationStore(Path.Combine(root, "different-mount"))
                .TryGet(original.DocumentId, out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
