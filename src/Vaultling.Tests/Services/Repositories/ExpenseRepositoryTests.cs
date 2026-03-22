using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class ExpenseRepositoryTests
{
    private static readonly string TestDataPath = Path.Combine("TestData", "expenses.csv");

    [Fact]
    public void ParseExpenseLogs_ReadsAllExpenseRows()
    {
        var lines = File.ReadLines(TestDataPath);
        var expenses = ExpenseRepository.ParseExpenseLogs(lines).ToList();

        Assert.Equal(4, expenses.Count);
    }

    [Fact]
    public void ParseExpenseLogs_SkipsHeader()
    {
        var lines = File.ReadLines(TestDataPath);
        var first = ExpenseRepository.ParseExpenseLogs(lines).First();

        Assert.Equal(1, first.Month);
        Assert.Equal(5, first.Day);
    }

    [Fact]
    public void ParseExpenseLogs_ParsesFieldsCorrectly()
    {
        var lines = File.ReadLines(TestDataPath);
        var expenses = ExpenseRepository.ParseExpenseLogs(lines).ToList();

        var first = expenses[0];
        Assert.Equal(1, first.Month);
        Assert.Equal(5, first.Day);
        Assert.Equal("food", first.Category);
        Assert.Equal(45.50m, first.Amount);
        Assert.Equal("groceries", first.Description);

        var last = expenses[3];
        Assert.Equal(2, last.Month);
        Assert.Equal(15, last.Day);
        Assert.Equal("utilities", last.Category);
        Assert.Equal(150.00m, last.Amount);
        Assert.Equal("electricity", last.Description);
    }

    [Fact]
    public void ToCsvLine_RoundTrips()
    {
        var lines = File.ReadLines(TestDataPath);
        var expense = ExpenseRepository.ParseExpenseLogs(lines).First();

        var csv = ExpenseRepository.ToCsvLine(expense);
        var reparsed = ExpenseRepository.ParseExpenseLogs(new[] { "header", csv }).Single();

        Assert.Equal(expense, reparsed);
    }
}
