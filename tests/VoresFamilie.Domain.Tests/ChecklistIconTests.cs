using VoresFamilie.Domain;

namespace VoresFamilie.Domain.Tests;

public class ChecklistIconTests
{
    [Theory]
    [InlineData("Børste tænder", "🪥")]
    [InlineData("Børst tænder", "🪥")]
    [InlineData("Vaske sig", "🧼")]
    [InlineData("Tage tøj på", "👕")]
    [InlineData("Spise morgenmad", "🥣")]
    [InlineData("Tage medicin", "💊")]
    [InlineData("Gå på toilet", "🚽")]
    [InlineData("Madpakke og drikkedunk i taksen", "🥪")]
    [InlineData("Sluk elektronik", "📱")]
    [InlineData("Børste hår", "💇")]
    [InlineData("Pak skoletaske", "🎒")]
    [InlineData("Lave aftensmad", "🍳")]
    [InlineData("Smøre madpakker", "🥪")]
    [InlineData("Tømme opvasker", "🍽️")]
    [InlineData("Rydde køkken", "🍽️")]
    [InlineData("Rydde værelse", "🧺")]
    [InlineData("Vaske tøj", "🧺")]
    [InlineData("Støvsuge", "🧹")]
    [InlineData("Tag skoene på", null)]
    [InlineData("Bær skrald ud", "🗑️")]
    [InlineData("TAG MEDICIN", "💊")]
    public void Suggests_an_icon_from_the_text(string title, string? expected) =>
        Assert.Equal(expected, ChecklistIcons.Suggest(title));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("Gør noget helt andet")]
    public void No_suggestion_when_nothing_fits(string? title) => Assert.Null(ChecklistIcons.Suggest(title));

    [Fact]
    public void School_words_do_not_trigger_the_shoe_icon() =>
        Assert.NotEqual("👟", ChecklistIcons.Suggest("Pak skoletaske"));

    [Fact]
    public void Stored_value_decides_what_is_shown()
    {
        Assert.Equal("🪥", ChecklistIcons.Effective(null, "Børste tænder"));  // automatic
        Assert.Null(ChecklistIcons.Effective("", "Børste tænder"));           // admin chose "no icon"
        Assert.Equal("🧼", ChecklistIcons.Effective("🧼", "Børste tænder"));  // admin chose another
    }

    [Fact]
    public void Item_stores_normalised_icon_and_rejects_nonsense()
    {
        var fam = Guid.NewGuid();
        Assert.Null(ChecklistItem.Create(fam, ChecklistKind.HouseholdTask, null, null, "x", DaysOfWeek.All, 0).Icon);
        Assert.Equal("🧼", ChecklistItem.Create(fam, ChecklistKind.HouseholdTask, null, null, "x", DaysOfWeek.All, 0, " 🧼 ").Icon);
        Assert.Equal("", ChecklistItem.Create(fam, ChecklistKind.HouseholdTask, null, null, "x", DaysOfWeek.All, 0, "  ").Icon);
        Assert.Throws<DomainException>(() =>
            ChecklistItem.Create(fam, ChecklistKind.HouseholdTask, null, null, "x", DaysOfWeek.All, 0, new string('a', 40)));
    }
}
