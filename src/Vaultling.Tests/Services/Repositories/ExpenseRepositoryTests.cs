using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class ExpenseRepositoryTests
{
    private static ExpenseRepository CreateRepository(string filePath) =>
        new ExpenseRepository(
            Options.Create(new ExpenseOptions { CurrentYearDataFile = filePath }));

    private static string WriteTempCsv(params string[] rows)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, ["month,day,category,amount,description", .. rows]);
        return tempFile;
    }

    [Fact]
    public void ReadExpenses_ReadsAllExpenseRows()
    {
        var tempFile = WriteTempCsv(
            "1,5,food,45.50,groceries",
            "1,12,transport,25.00,bus pass",
            "2,3,food,30.00,restaurant",
            "2,15,utilities,150.00,electricity");
        try
        {
            var expenses = CreateRepository(tempFile).ReadCurrentYearExpenses().ToList();
            Assert.Equal(4, expenses.Count);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void ReadExpenses_SkipsHeader()
    {
        var tempFile = WriteTempCsv("1,5,food,45.50,groceries");
        try
        {
            var first = CreateRepository(tempFile).ReadCurrentYearExpenses().First();
            Assert.Equal(1, first.Month);
            Assert.Equal(5, first.Day);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void ReadExpenses_ParsesFieldsCorrectly()
    {
        var tempFile = WriteTempCsv(
            "1,5,food,45.50,groceries",
            "2,15,utilities,150.00,electricity");
        try
        {
            var expenses = CreateRepository(tempFile).ReadCurrentYearExpenses().ToList();

            var first = expenses[0];
            Assert.Equal(1, first.Month);
            Assert.Equal(5, first.Day);
            Assert.Equal("food", first.Category);
            Assert.Equal(45.50m, first.Amount);
            Assert.Equal("groceries", first.Description);

            var last = expenses[1];
            Assert.Equal(2, last.Month);
            Assert.Equal(15, last.Day);
            Assert.Equal("utilities", last.Category);
            Assert.Equal(150.00m, last.Amount);
            Assert.Equal("electricity", last.Description);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void AppendExpenses_ThenReadExpenses_RoundTrips()
    {
        var tempFile = WriteTempCsv();
        try
        {
            var repository = CreateRepository(tempFile);
            var expense = new ExpenseLog(3, 7, "food", 45.50m, "groceries");
            repository.AppendExpenses([expense]);

            var reparsed = repository.ReadCurrentYearExpenses().Single();
            Assert.Equal(expense, reparsed);
        }
        finally { File.Delete(tempFile); }
    }
}
