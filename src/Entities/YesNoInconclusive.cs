using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Entities {
    public class YesNoInconclusive : IYesNoInconclusive {
        public bool YesNo { get; set; }
        public bool Inconclusive { get; set; }
    }
}
