﻿namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;

public class BinaryToUpdate {
    public string FileName { get; set; }
    public string UpdateReason { get; set; }

    public override string ToString() {
        return $"{FileName} ({UpdateReason})";
    }
}