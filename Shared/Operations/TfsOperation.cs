using System.ComponentModel;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
#endif
using Inedo.Extensions.TFS.Credentials;

namespace Inedo.Extensions.TFS.Operations
{
    public abstract class TfsOperation : ExecuteOperation, IHasCredentials<TfsCredentials>, IVsoConnectionInfo
    {
        [Category("Connection/Identity")]
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Url")]
        [DisplayName("Project collection URL")]
        [MappedCredential(nameof(TfsCredentials.TeamProjectCollection))]
        public string TeamProjectCollectionUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [MappedCredential(nameof(TfsCredentials.UserName))]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password / token")]
        [MappedCredential(nameof(TfsCredentials.PasswordOrToken))]
        public string PasswordOrToken { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Domain")]
        [DisplayName("Domain name")]
        [MappedCredential(nameof(TfsCredentials.Domain))]
        public string Domain { get; set; }
    }
}