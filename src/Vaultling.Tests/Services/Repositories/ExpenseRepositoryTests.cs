using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class ExpenseRepositoryTests
{
    private static readonly string TestDataPath = Path.Combine("TestData", "expenses.csv");
    private readonly ExpenseRepository _repository;
    private readonly List<ExpenseLog> _expenses;

    public ExpenseRepositoryTests()
    {
        _repository = new ExpenseRepository(
            Options.Create(new ExpenseOptions { DataFile = TestDataPath }));
        _expenses = _repository.ReadExpenses().ToList();
    }

    [Fact]
    public void ReadExpenses_ReadsAllExpenseRows()
    {
        Assert.Equal(4, _expenses.Count);
    }

    [Fact]
    public void ReadExpenses_SkipsHeader()
    {
        var first = _expenses.First();

        Assert.Equal(1, first.Month);
        Assert.Equal(5, first.Day);
    }

    [Fact]
    public void ReadExpenses_ParsesFieldsCorrectly()
    {
        var first = _expenses[0];
        Assert.Equal(1, first.Month);
        Assert.Equal(5, first.Day);
        Assert.Equal("food", first.Category);
        Assert.Equal(45.50m, first.Amount);
        Assert.Equal("groceries", first.Description);

        var last = _expenses[3];
        Assert.Equal(2, last.Month);
        Assert.Equal(15, last.Day);
        Assert.Equal("utilities", last.Category);
        Assert.Equal(150.00m, last.Amount);
        Assert.Equal("electricity", last.Description);
    }

    [Fact]
    public void ToCsvLine_RoundTrips()
    {
        var expense = _expenses.First();

        var csv = ExpenseRepository.ToCsvLine(expense);
        var parts = csv.Split(',');
        var reparsed = new ExpenseLog(
            Month: int.Parse(parts[0]),
            Day: int.Parse(parts[1]),
            Category: parts[2],
            Amount: decimal.Parse(parts[3]),
            Description: parts[4]);

        Assert.Equal(expense, reparsed);
    }
}
