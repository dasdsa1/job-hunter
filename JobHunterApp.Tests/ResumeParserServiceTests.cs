using JobHunterApp.Services;

namespace JobHunterApp.Tests;

public class ResumeParserServiceTests
{
    [Fact]
    public async Task ParseAsync_UnsupportedFileType_Txt_ThrowsNotSupportedException()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => ResumeParserService.ParseAsync("resume.txt"));
        Assert.Contains("Unsupported file type", ex.Message);
    }

    [Fact]
    public async Task ParseAsync_UnsupportedFileType_Doc_ThrowsNotSupportedException()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => ResumeParserService.ParseAsync("resume.doc"));
        Assert.Contains("Unsupported file type", ex.Message);
    }

    [Fact]
    public async Task ParseAsync_UnsupportedFileType_Png_ThrowsNotSupportedException()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => ResumeParserService.ParseAsync("resume.png"));
        Assert.Contains("Unsupported file type", ex.Message);
    }
}
