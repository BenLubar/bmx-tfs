using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Otter;
using Inedo.Otter.Extensibility.RaftRepositories;
using Inedo.Otter.Extensibility.UserDirectories;
using Inedo.Serialization;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;

namespace Inedo.Extensions.TFS.RaftRepositories
{
    [DisplayName("TFS")]
    [Description("This raft is persisted in a Team Foundation Server version control repository.")]
    public sealed class TfsRaftRepository : RaftRepository
    {
        public TfsRaftRepository()
        {
            this.Connection = new Lazy<TfsTeamProjectCollection>(OpenConnection);
            this.Workspace = new Lazy<Workspace>(OpenWorkspace);
        }

        [Required]
        [Persistent]
        [DisplayName("Remote repository URL")]
        [PlaceholderText("TFS team project collection URL")]
        public string BaseUrl { get; set; }
        private Uri BaseUri => new Uri(this.BaseUrl);

        [Persistent]
        [DisplayName("Username")]
        public string Username { get; set; }

        [Persistent(Encrypted = true)]
        public SecureString Password { get; set; }

        [Required]
        [Persistent]
        [DisplayName("Workspace Name")]
        public string WorkspaceName { get; set; }

        [Persistent]
        [DisplayName("Use system credentials")]
        public bool UseSystemCredentials { get; set; }

        private string PathPrefix => "$/" + this.WorkspaceName;

        public override bool IsReadOnly => !this.Workspace.Value.HasCheckInPermission;

        private Lazy<TfsTeamProjectCollection> Connection;
        private Lazy<Workspace> Workspace;

        private TfsTeamProjectCollection OpenConnection()
        {
            var connection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(this.BaseUri);
            if (!this.UseSystemCredentials)
            {
                connection.ClientCredentials = new TfsClientCredentials(new WindowsCredential(new NetworkCredential(this.Username, this.Password)));
            }
            connection.EnsureAuthenticated();
            return connection;
        }

        private Workspace OpenWorkspace()
        {
            var server = this.Connection.Value.GetService<VersionControlServer>();

            var workspaces = server.QueryWorkspaces(this.WorkspaceName, server.AuthorizedUser, Environment.MachineName);
            var workspace = workspaces.FirstOrDefault();
            if (workspace == null)
            {
                workspace = server.CreateWorkspace(this.WorkspaceName);
            }

            var localPath = Path.Combine(OtterConfig.Extensions.ServiceTempPath, "TFS-Rafts", this.RaftName);
            if (!workspace.IsLocalPathMapped(localPath))
            {
                workspace.Map(this.PathPrefix, localPath);
            }

            workspace.Get(VersionSpec.Latest, GetOptions.Overwrite);

            return workspace;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.Connection.IsValueCreated)
                {
                    this.Connection.Value.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public override void Commit(IUserDirectoryUser user)
        {
            this.Workspace.Value.CheckIn(this.Workspace.Value.GetPendingChanges(), $"Updated by Otter user {user.DisplayName}.");
        }

        private static readonly string[] RaftItemTypes = Enum.GetValues(typeof(RaftItemType)).Cast<RaftItemType>().Select(GetStandardTypeName).ToArray();

        public override IEnumerable<RaftItem> GetRaftItems()
        {
            var itemSets = this.Workspace.Value.GetItems(ItemSpec.FromStrings(RaftItemTypes.Select(x => this.PathPrefix + "/" + x + "*.otter").ToArray(), RecursionType.OneLevel),
                DeletedState.NonDeleted, ItemType.File, false, GetItemsOptions.None);
            foreach (var itemSet in itemSets)
            {
                foreach (var item in itemSet.Items)
                {
                    var type = TryParseStandardTypeName(Path.GetFileName(Path.GetDirectoryName(item.ServerItem)));
                    if (type != null)
                    {
                        yield return new RaftItem(type.Value, Path.GetFileNameWithoutExtension(item.ServerItem), item.CheckinDate);
                    }
                }
            }
        }

        public override IEnumerable<RaftItem> GetRaftItems(RaftItemType type)
        {
            var itemSets = this.Workspace.Value.GetItems(ItemSpec.FromStrings(new[] { this.PathPrefix + "/" + GetStandardTypeName(type) + "/*.otter" }, RecursionType.None),
                DeletedState.NonDeleted, ItemType.File, false, GetItemsOptions.None);

            foreach (var itemSet in itemSets)
            {
                foreach (var item in itemSet.Items)
                {
                    yield return new RaftItem(type, Path.GetFileNameWithoutExtension(item.ServerItem), item.CheckinDate);
                }
            }
        }

        public override RaftItem GetRaftItem(RaftItemType type, string name)
        {
            var itemSets = this.Workspace.Value.GetItems(ItemSpec.FromStrings(new[] { this.PathPrefix + "/" + GetStandardTypeName(type) + "/" + name + ".otter" }, RecursionType.None),
                DeletedState.NonDeleted, ItemType.File, false, GetItemsOptions.None);

            foreach (var itemSet in itemSets)
            {
                foreach (var item in itemSet.Items)
                {
                    return new RaftItem(type, name, item.CheckinDate);
                }
            }

            return null;
        }

        public override Stream OpenRaftItem(RaftItemType type, string name, FileMode fileMode, FileAccess fileAccess)
        {
            var path = this.PathPrefix + "/" + GetStandardTypeName(type) + "/" + name + ".otter";
            var localPath = this.Workspace.Value.GetLocalItemForServerItem(path);
            if (fileAccess != FileAccess.Read)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                if (!File.Exists(localPath))
                {
                    this.Workspace.Value.PendAdd(new[] { path }, false, FileType.AutoFileType, LockLevel.Checkin, true, false);
                }
                else
                {
                    this.Workspace.Value.PendEdit(new[] { path },RecursionType.None, FileType.AutoFileType, LockLevel.Checkin, false);
                }
            }
            else if (!File.Exists(localPath))
            {
                return null;
            }
            return new FileStream(localPath, fileMode, fileAccess);
        }

        public override void DeleteRaftItem(RaftItemType type, string name)
        {
            var path = this.PathPrefix + "/" + GetStandardTypeName(type) + "/" + name + ".otter";
            this.Workspace.Value.PendDelete(path);
        }

        public override IReadOnlyDictionary<RuntimeVariableName, string> GetVariables()
        {
            try
            {
                using (var reader = new StreamReader(this.Workspace.Value.GetLocalItemForServerItem(this.PathPrefix + "/variables")))
                {
                    return ReadStandardVariableData(reader);
                }
            }
            catch (FileNotFoundException)
            {
                return new Dictionary<RuntimeVariableName, string>();
            }
        }

        public override bool DeleteVariable(RuntimeVariableName name)
        {
            var variables = this.GetVariables().ToDictionary(v => v.Key, v => v.Value);
            if (variables.Remove(name))
            {
                this.SaveVariables(variables);
                return true;
            }
            return false;
        }

        public override void SetVariable(RuntimeVariableName name, string value)
        {
            var variables = this.GetVariables().ToDictionary(v => v.Key, v => v.Value);
            variables.Add(name, value);
            this.SaveVariables(variables);
        }

        private void SaveVariables(IReadOnlyDictionary<RuntimeVariableName, string> variables)
        {
            var localPath = this.Workspace.Value.GetLocalItemForServerItem(this.PathPrefix + "/variables");
            if (File.Exists(localPath))
            {
                this.Workspace.Value.PendEdit(this.PathPrefix + "/variables");
            }
            else
            {
                this.Workspace.Value.PendAdd(this.PathPrefix + "/variables");
            }
            using (var writer = new StreamWriter(localPath))
            {
                WriteStandardVariableData(variables, writer);
            }
        }
    }
}
