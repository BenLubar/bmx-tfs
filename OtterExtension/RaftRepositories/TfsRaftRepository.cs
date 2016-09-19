using Inedo.ExecutionEngine;
using Inedo.Otter;
using Inedo.Otter.Extensibility.RaftRepositories;
using Inedo.Otter.Extensibility.UserDirectories;
using Inedo.Serialization;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;

namespace Inedo.Extensions.TFS.RaftRepositories
{
    public class TfsRaftRepository : RaftRepository
    {
        public TfsRaftRepository()
        {
            this.Connection = new Lazy<TfsConfigurationServer>(OpenConnection);
            this.Workspace = new Lazy<Workspace>(OpenWorkspace);
        }

        [Persistent]
        public string BaseUrl { get; set; }
        protected Uri BaseUri => new Uri(this.BaseUrl);
        [Persistent]
        public string Username { get; set; }
        [Persistent]
        public SecureString Password { get; set; }

        public override bool IsReadOnly => false;

        private Lazy<TfsConfigurationServer> Connection;
        private Lazy<Workspace> Workspace;

        private TfsConfigurationServer OpenConnection()
        {
            var connection = TfsConfigurationServerFactory.GetConfigurationServer(this.BaseUri);
            connection.ClientCredentials = new TfsClientCredentials(new WindowsCredential(new NetworkCredential(this.Username, this.Password)));
            connection.EnsureAuthenticated();
            return connection;
        }

        private Workspace OpenWorkspace()
        {
            var server = this.Connection.Value.GetService<VersionControlServer>();
            var localPath = Path.Combine(OtterConfig.Extensions.ServiceTempPath, "TFS-Rafts", this.RaftName);
            var workspace = server.TryGetWorkspace(localPath);
            if (workspace == null)
            {
                workspace = server.CreateWorkspace(localPath);
            }
            workspace.Refresh();
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
            this.Workspace.Value.CheckIn(null, $"Updated by Otter user {user.DisplayName}.");
        }

        private static readonly string[] RaftItemTypes = Enum.GetValues(typeof(RaftItemType)).Cast<RaftItemType>().Select(GetStandardTypeName).ToArray();

        public override IEnumerable<RaftItem> GetRaftItems()
        {
            var itemSets = this.Workspace.Value.GetItems(ItemSpec.FromStrings(RaftItemTypes, RecursionType.OneLevel),
                DeletedState.NonDeleted, ItemType.File, false, 0);
            foreach (var itemSet in itemSets)
            {
                foreach (var item in itemSet.Items)
                {
                    yield return new RaftItem(TryParseStandardTypeName(Path.GetDirectoryName(item.ServerItem)).Value, Path.GetFileName(item.ServerItem), item.CheckinDate);
                }
            }
        }

        public override IEnumerable<RaftItem> GetRaftItems(RaftItemType type)
        {
            var itemSets = this.Workspace.Value.GetItems(ItemSpec.FromStrings(new[] { GetStandardTypeName(type) }, RecursionType.OneLevel),
                DeletedState.NonDeleted, ItemType.File, false, 0);

            foreach (var itemSet in itemSets)
            {
                foreach (var item in itemSet.Items)
                {
                    yield return new RaftItem(type, Path.GetFileName(item.ServerItem), item.CheckinDate);
                }
            }
        }

        public override RaftItem GetRaftItem(RaftItemType type, string name)
        {
            var itemSets = this.Workspace.Value.GetItems(ItemSpec.FromStrings(new[] { Path.Combine(GetStandardTypeName(type), name) }, RecursionType.None),
                DeletedState.NonDeleted, ItemType.File, false, 0);

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
            if (fileAccess != FileAccess.Read)
            {
                Directory.CreateDirectory(this.Workspace.Value.GetLocalItemForServerItem(GetStandardTypeName(type)));
                this.Workspace.Value.PendEdit(Path.Combine(GetStandardTypeName(type), name));
            }
            return new FileStream(this.Workspace.Value.GetLocalItemForServerItem(Path.Combine(GetStandardTypeName(type), name)), fileMode, fileAccess);
        }

        public override void DeleteRaftItem(RaftItemType type, string name)
        {
            this.Workspace.Value.PendDelete(Path.Combine(GetStandardTypeName(type), name));
        }

        public override IReadOnlyDictionary<RuntimeVariableName, string> GetVariables()
        {
            try
            {
                using (var reader = new StreamReader(this.Workspace.Value.GetLocalItemForServerItem("variables")))
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
            var localPath = this.Workspace.Value.GetLocalItemForServerItem("variables");
            if (File.Exists(localPath))
            {
                this.Workspace.Value.PendEdit("variables");
            }
            else
            {
                this.Workspace.Value.PendAdd("variables");
            }
            using (var writer = new StreamWriter(localPath))
            {
                WriteStandardVariableData(variables, writer);
            }
        }
    }
}
