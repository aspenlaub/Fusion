using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TechTalk.SpecFlow;
using TableRow = TechTalk.SpecFlow.TableRow;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test {
    [Binding]
    public class JsonDepsDifferencerSteps {
        private IJsonDepsDifferencer vSut;

        [When(@"I instantiate a json dependency differencer")]
        public void WhenIInstantiateAJsonDependencyDifferencer() {
            vSut = new JsonDepsDifferencer();
        }

        [Then(@"it determines the following update necessities")]
        public void ThenItDeterminesTheFollowingUpdateNecessities(Table table) {
            foreach (var tableRow in table.Rows) {
                CheckRow(tableRow);
            }
        }

        private void CheckRow(TableRow tableRow) {
            var oldJson = tableRow["Old Json"];
            var newJson = tableRow["New Json"];
            var nameSpace = tableRow["Namespace"];
            var expectedToBeIdentical = string.IsNullOrWhiteSpace(tableRow["Necessary"]);
            var actuallyIdentical = vSut.AreJsonDependenciesIdenticalExceptForNamespaceVersion(oldJson, newJson, nameSpace, out string updateReason);
            var errorMessage = expectedToBeIdentical
                ? $"Old json '{oldJson}' and new json '{newJson}' should be identical in the context of namespace '{nameSpace}' ({updateReason})"
                : $"Old json '{oldJson}' and new json '{newJson}' should not be identical in the context of namespace '{nameSpace}'";
            Assert.AreEqual(expectedToBeIdentical, actuallyIdentical, errorMessage);
        }
    }
}
