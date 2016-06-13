﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Inedo.BuildMasterExtensions.TFS
{
    internal sealed class TfsSourceControlContext : SourceControlContext
    {
        private static readonly Regex WorkspaceNameSanitizerRegex = new Regex("[" + Regex.Escape(@"""/:<>\|*?;") + "]", RegexOptions.Compiled);
        private const string EmptyPathString = "$/";

        public TfsSourceControlContext(TfsSourceControlProvider provider, string sourcePath)
            : this(provider, sourcePath, null)
        {
        }
        public TfsSourceControlContext(TfsSourceControlProvider provider, string sourcePath, string label)
        {
            this.Label = label;

            if (string.IsNullOrEmpty(sourcePath))
                this.SourcePath = EmptyPathString;
            else
                this.SourcePath = sourcePath.TrimStart(provider.DirectorySeparator);

            this.SplitPath = SplitPathParts(this.SourcePath);
            this.LastSubDirectoryName = this.SplitPath.LastOrDefault() ?? string.Empty;

            var tmpRepo = new SourceRepository() { RemoteUrl = BuildAbsoluteDiskPath(provider.BaseUrl, this.SplitPath) };

            if (string.IsNullOrEmpty(provider.CustomWorkspacePath))
                this.WorkspaceDiskPath = tmpRepo.GetDiskPath(provider.Agent.GetService<IFileOperationsExecuter>());
            else
                this.WorkspaceDiskPath = provider.CustomWorkspacePath;

            if (string.IsNullOrEmpty(provider.CustomWorkspaceName))
                this.WorkspaceName = BuildWorkspaceName(this.WorkspaceDiskPath);
            else
                this.WorkspaceName = provider.CustomWorkspaceName;
        }

        public string SourcePath { get; }
        public string[] SplitPath { get; }
        public string LastSubDirectoryName { get; }
        public string WorkspaceName { get; }

        internal SystemEntryInfo CreateSystemEntryInfo(Item item)
        {
            string name = SplitPathParts(item.ServerItem).LastOrDefault() ?? string.Empty;

            if (item.ItemType == ItemType.Folder)
                return new DirectoryEntryInfo(name, item.ServerItem);
            else
                return new FileEntryInfo(name, item.ServerItem);
        }

        private string BuildAbsoluteDiskPath(string path1, string[] parts)
        {
            return (path1 ?? "").TrimEnd() + @"\" + string.Join(@"\", parts);
        }

        private static string[] SplitPathParts(string sourcePath)
        {
            return sourcePath
                .Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "$")
                .ToArray();
        }

        private static string BuildWorkspaceName(string workspaceDiskPath)
        {
            /* 
             * Workspace naming restrictions:
             *  - Max length of 64 Unicode characters
             *  - Cannot end with a space
             *  - Must not contain the following printable characters: " / : < > \ | * ? ;      
             */

            var split = workspaceDiskPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string name = "BM-" + split.LastOrDefault();
            name = WorkspaceNameSanitizerRegex.Replace(name, "_");
            name = name.Trim();
            if (name.Length > 64)
                return name.Substring(0, 64);
            else
                return name;
        }
    }
}
