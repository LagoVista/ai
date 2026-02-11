using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityHeaderIndexBuilder;

internal static class Program
{

    private static int Main(string[] args)
    {
        var root = args.Length > 0 ? args[0] : @"D:\NuvIoT";
        var outPath = Path.Combine(root, "Output", "entity-header-map.csv");
        var outPathEntityDescription = Path.Combine(root, "Output", "entity-description.csv");
        var outRoutePath = Path.Combine(root, "Output", "route-map.csv");

        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Root directory not found: {root}");
            return 2;
        }

      //  EntityHeaderFormFieldIndexBuilder.Run(root, outPath);
        EntityDescriptionUrlIndexBuilder.Run(root, outPathEntityDescription);
      //  ControllerRouteIndexBuilder.Run(root, outRoutePath);
        return 0;
    }
}