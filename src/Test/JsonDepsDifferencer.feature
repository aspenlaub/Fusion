@JsonDepsDifferencer
Feature: JsonDepsDifferencer
	In order to update compiler json dependencies output only if necessary
	I want to use a dedicated json dependencies differencer

Scenario: Differences can be identified
    When I instantiate a json dependency differencer
    Then it determines the following update necessities
    | Old Json                                                | New Json                                                | Namespace                                | Necessary |
    | "Aspenlaub.Net.GitHub.CSharp.ChabStandard/2.0.94.817"   | "Aspenlaub.Net.GitHub.CSharp.ChabStandard/2.0.95.892"   | Aspenlaub.Net.GitHub.CSharp.ChabStandard |           |
    | "Aspenlaub.Net.GitHub.CSharp.ChabStandard/2.0.94.817"   | "Aspenlaub.Net.GitHub.CSharp.ChabStandard/2.0.95.892"   | Aspenlaub.Net.GitHub.CSharp.ChabStandar  | X         |
    | "Aspenlaub.Net.GitHub.CSharp.ChabStandard/2.0.94.817"   | "Aspenlaub.Net.GitHub.CSharp.ChabStandar/2.0.95.892"    | Aspenlaub.Net.GitHub.CSharp.ChabStandard | X         |
    | "Aspenlaub.Net.GitHub.CSharp.ChabStandar/2.0.94.817"    | "Aspenlaub.Net.GitHub.CSharp.ChabStandard/2.0.95.892"   | Aspenlaub.Net.GitHub.CSharp.ChabStandard | X         |
    | "Aspenlaub.Net.GitHub.CSharp.ChabStandard/2.0.94.817" { | "Aspenlaub.Net.GitHub.CSharp.ChabStandard/2.0.95.892" [ | Aspenlaub.Net.GitHub.CSharp.ChabStandard | X         |
    | "ChabStandard/2.0.94.817"                               | "ChabStandard/2.0.95.892"                               | Aspenlaub.Net.GitHub.CSharp.ChabStandard |           |
    | "ChabStandard/2.0.94.817"                               | "ChabStandard/2.0.95.892"                               | Aspenlaub.Net.GitHub.CSharp.ChabStandar  | X         |
    | "ChabStandard/2.0.94.817"                               | "ChabStandar/2.0.95.892"                                | Aspenlaub.Net.GitHub.CSharp.ChabStandard | X         |
    | "ChabStandar/2.0.94.817"                                | "ChabStandard/2.0.95.892"                               | Aspenlaub.Net.GitHub.CSharp.ChabStandard | X         |
    | "ChabStandard/2.0.94.817" {                             | "ChabStandard/2.0.95.892" [                             | Aspenlaub.Net.GitHub.CSharp.ChabStandard | X         |