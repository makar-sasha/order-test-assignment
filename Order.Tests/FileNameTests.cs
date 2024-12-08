using Order.Core.Orders;
using Order.Infrastructure.Orders;

namespace Order.Tests;

public class FileNameTests
{
    private readonly IFileName _fileName;

    public FileNameTests()
    {
        _fileName = new FileName();
    }

    [Theory]
    [InlineData("", "Unknown")]
    [InlineData("valid_file_name.txt", "valid_file_name.txt")]
    public void Sanitize_ShouldReturnExpectedResult(string input, string expected)
    {
        var result = _fileName.Sanitize(input);
        
        Assert.Equal(expected, result);
    }
}