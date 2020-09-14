using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace NugetPackageReferenceCleanup
{
    class Program
    {
        private static void FindUnusedReferences(string rootPath, bool cleanupProjectConfig)
        {
            var ignorePackagesList = new List<string>() { "Stamp.Fody" };
            var directory = new DirectoryInfo(rootPath);
            var packageConfigurations = directory.GetFiles("packages.config", SearchOption.AllDirectories);

            foreach (var config in packageConfigurations)
            {
                var configFilePath = config.FullName;
                var projectFolder = config.Directory;
                var projectFilePath = projectFolder.GetFiles("*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault().FullName;
                var projectFile = File.ReadAllText(projectFilePath).ToLowerInvariant();

                var nugetProjects = File.ReadAllText(configFilePath);

                //var projectXmlDoc = new XmlDocument();
                //projectXmlDoc.LoadXml(File.ReadAllText(projectFilePath));
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(nugetProjects);

                var xmlNodeList = xmlDoc.GetElementsByTagName("package");
                var packagesNotFound = string.Empty;
                var nodesToRemove = new List<XmlNode>();

                foreach (XmlNode xmlNode in xmlNodeList)
                {
                    var nugetPackage = xmlNode.Attributes["id"].Value;

                    if (ignorePackagesList.IndexOf(nugetPackage) < 0)
                    {
                        var find = @"packages\" + nugetPackage.ToLowerInvariant();

                        if (!projectFile.Contains(find))
                        {
                            packagesNotFound += nugetPackage + "\r\n";

                            if (cleanupProjectConfig)
                            {
                                nodesToRemove.Add(xmlNode);
                            }
                        }
                    }
                    //projectXmlDoc.SelectNodes("//ItemGroup/Reference/HintPath/*[@*[contains(.,'TEXT')]]");
                    //var list = projectXmlDoc.SelectNodes("//ItemGroup/Reference/*[@*[contains(.,'" + nugetPackage + "')]]");
                    //Console.WriteLine(nugetPackage);
                }

                if (!string.IsNullOrEmpty(packagesNotFound))
                {
                    Console.WriteLine(projectFolder.FullName);
                    Console.WriteLine(packagesNotFound);

                    if (cleanupProjectConfig)
                    {
                        var packagesNode = xmlDoc.SelectSingleNode("//packages");

                        foreach (var xmlNode in nodesToRemove)
                        {
                            packagesNode.RemoveChild(xmlNode);
                        }

                        Console.WriteLine("Cleaned up - " + configFilePath);
                        xmlDoc.Save(configFilePath);
                    }

                    Console.WriteLine("******");
                }
            }

            Console.WriteLine("Done...");
            Console.ReadLine();
        }

        static void Main(string[] args)
        {
            var rootPath = args.Length > 0 ? args[0] : @"C:\maine\Solution";
            var cleanupProjectConfig = args.Length > 1 ? args[1].Equals("true", StringComparison.InvariantCultureIgnoreCase) : true;

            Console.WriteLine(rootPath);
            FindUnusedReferences(rootPath, cleanupProjectConfig);
        }
    }
}
