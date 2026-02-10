using Ymir.GeminiSync.Services.ManualTests;

namespace Ymir.GeminiSync.Services.UnitTests
{
    public class DateSplitTests
    {
        [Fact]
        public async Task GroupByGarbageBins_Old()
        {
            //Arrange
            const string filePath = "Data\\Agreements_40004.json";
            var collectionLines = await FileUtils.ReadGarbageBinListAsync(filePath);

            //Act
            var states = GeminiUtils.SplitByDates(collectionLines);

            //Assert
            Assert.Fail("Unfinished test");
        }

        [Fact]
        public async Task GroupByGarbageBins()
        {
            //Arrange
            const string filePath = "Data\\Agreements_40004.json";
            var collectionLines = await FileUtils.ReadGarbageBinListAsync(filePath);

            //Act
            var states = GeminiUtils.BuildAgreementIntervalsByDate(collectionLines);

            //Assert
            Assert.Fail("Unfinished test");
        }
    }
}
