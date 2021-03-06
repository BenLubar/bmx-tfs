﻿using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Extensions.TFS.Operations;
using Inedo.Extensions.TFS.VisualStudioOnline;

namespace Inedo.Extensions.TFS.Legacy.ActionImporters
{
    internal sealed class QueueVsoBuildImporter : IActionOperationConverter<QueueVsoBuildAction, QueueVsoBuildOperation>
    {
        public ConvertedOperation<QueueVsoBuildOperation> ConvertActionToOperation(QueueVsoBuildAction action, IActionConverterContext context)
        {
            var configurer = (TfsConfigurer)context.Configurer;
            
            return new QueueVsoBuildOperation
            {
                BuildDefinition = action.BuildDefinition,
                UserName = configurer.UserName,
                PasswordOrToken = configurer.Password,
                Domain = configurer.Domain,
                TeamProjectCollectionUrl = configurer.BaseUrl,
                TeamProject = action.TeamProject,
                CreateBuildNumberVariable = action.CreateBuildNumberVariable,
                ValidateBuild = action.ValidateBuild,
                WaitForCompletion = action.WaitForCompletion
            };
        }
    }
}
