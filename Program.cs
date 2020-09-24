using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace NugetPackageReferenceCleanup
{
    class NugetPackage
    {
        public NugetPackage(string nugetPackagePath)
        {
            var ignorePackagesList = new List<string>()
            {
                "Sitecore.Support.257244.1.0.0",
                "Sitecore.Support.96030.MediaImageDimensions.Fix.8.2.0",
                "Sitecore.Support.102970.4.0.0",
                "Sitecore.Support.115854.9.0.2",
                "Sitecore.Support.126958.1.0.0",
                "Sitecore.Support.156916.1.1.3",
                "Sitecore.Support.158936.9.0.2.3",
                "Sitecore.Support.159068.ClearRenderingParamFix.9.0.2",
                "Sitecore.Support.160614.173951.IESendEmailEditor.9.0.2",
                "Sitecore.Support.198665.RichTextEditorIEFix.9.0.2",
                "Sitecore.Support.208634.9.0.1",
                "Sitecore.Support.212651.9.0.2",
                "Sitecore.Support.223702.9.0.180604.1",
                "Sitecore.Support.228036.1.0.2",
                "Sitecore.Support.257244.1.0.0",
                "Sitecore.Support.302938.SecurityVulnerability.9.0.1.1"
            };

            var index = nugetPackagePath.IndexOf(@"\packages\");
            this.Path = nugetPackagePath;

            if (index >= 0)
            {
                var pacakge = nugetPackagePath.Substring(index + 10);
                index = pacakge.IndexOf(@"\");
                pacakge = pacakge.Substring(0, index);

                if (ignorePackagesList.IndexOf(pacakge) < 0)
                {
                    var parts = pacakge.Split(new char[] { '.' });
                    var packageParts = new List<string>();
                    var versionParts = new List<string>();

                    foreach (var part in parts)
                    {
                        var version = 0;

                        if (int.TryParse(part, out version))
                        {
                            versionParts.Add(part);
                        }
                        else
                        {
                            packageParts.Add(part);
                        }
                    }

                    this.Package = string.Join(".", packageParts.ToArray());
                    this.Version = string.Join(".", versionParts.ToArray());
                }
            }
        }
        public string Path { get; set; }
        public string Package { get; set; }
        public string Version { get; set; }
    }
    class Program
    {
        private static void FindAndFixMissingPackages(string rootPath, bool addMissingPackages)
        {
            var directory = new DirectoryInfo(rootPath);
            var packageConfigurations = directory.GetFiles("packages.config", SearchOption.AllDirectories);

            foreach (var config in packageConfigurations)
            {
                var configFilePath = config.FullName;
                var projectFolder = config.Directory;
                var projectFilePath = projectFolder.GetFiles("*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault().FullName;

                var nugetProjects = File.ReadAllText(configFilePath);
                var projectXmlDoc = new XmlDocument();
                projectXmlDoc.LoadXml(File.ReadAllText(projectFilePath));
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(nugetProjects);

                var xmlNodeList = projectXmlDoc.GetElementsByTagName("HintPath");
                var missingPackagesErrors = new List<string>();
                var missingPackages = new List<NugetPackage>();

                foreach (XmlNode xmlNode in xmlNodeList)
                {
                    var nugetPackagePath = xmlNode.InnerText;

                    var index = nugetPackagePath.IndexOf(@"\packages\");

                    if (index >= 0)
                    {
                        var nuget = new NugetPackage(nugetPackagePath);

                        if (!string.IsNullOrEmpty(nuget.Package))
                        {
                            var packageNode = xmlDoc.SelectSingleNode("//packages/package[@id='" + nuget.Package + "']");

                            if (packageNode == null)
                            {
                                var error = string.Format("**Package not found. {0} version {1}", nuget.Package, nuget.Version);

                                if(missingPackagesErrors.IndexOf(error) < 0)
                                {
                                    missingPackages.Add(nuget);
                                    missingPackagesErrors.Add(error);
                                }
                            }
                            else if (packageNode.Attributes["version"].Value != nuget.Version)
                            {
                                missingPackagesErrors.Add(string.Format("*Different package version. {0} project version {1}. package version {2}", nuget.Package, nuget.Version, packageNode.Attributes["version"].Value));
                            }
                        }
                    }
                }

                if (missingPackagesErrors.Any())
                {
                    var packages = xmlDoc.SelectSingleNode("//packages");
                    
                    foreach (var nuget in missingPackages)
                    {
                        var package = xmlDoc.CreateElement("package");
                        package.SetAttribute("id", nuget.Package);
                        package.SetAttribute("version", nuget.Version);
                        package.SetAttribute("targetFramework", "net472");
                        packages.AppendChild(package);
                    }

                    if (addMissingPackages)
                    {
                        xmlDoc.Save(configFilePath);
                    }

                    Console.WriteLine(projectFolder.FullName);
                    foreach (var error in missingPackagesErrors)
                    {
                        Console.WriteLine(error);
                    }
                    Console.WriteLine("******");
                }
            }

            Console.WriteLine("Done...");
        }

        private static void FindAndFixUnusedReferences(string rootPath, bool cleanupProjectConfig)
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
        }

        static void Main(string[] args)
        {
            var rootPath = args.Length > 0 ? args[0] : @"C:\maine\Solution";
            //var cleanupProjectConfig = args.Length > 1 ? args[1].Equals("true", StringComparison.InvariantCultureIgnoreCase) : true;
            var cleanupProjectConfig = true;
            var addMissingPackages = true;

            Console.WriteLine(rootPath);

            Console.WriteLine("****FindAndFixUnusedReferences****");
            FindAndFixUnusedReferences(rootPath, cleanupProjectConfig);

            Console.WriteLine("****FindAndFixMissingPackages****");
            FindAndFixMissingPackages(rootPath, addMissingPackages);

            Console.ReadLine();
        }
    }
}
