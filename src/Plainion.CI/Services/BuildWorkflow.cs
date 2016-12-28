﻿using System;
using System.IO;
using System.Threading.Tasks;
using Plainion.CI.Services.SourceControl;

namespace Plainion.CI.Services
{
    internal class BuildWorkflow
    {
        private ISourceControl mySourceControl;
        private BuildDefinition myDefinition;
        private BuildRequest myRequest;

        public BuildWorkflow( ISourceControl sourceControl, BuildDefinition definition, BuildRequest request )
        {
            mySourceControl = sourceControl;
            myDefinition = definition;
            myRequest = request;
        }

        internal Task<bool> ExecuteAsync( IProgress<string> progress )
        {
            // clone thread save copy of the relevant paramters;
            myDefinition = Objects.Clone( myDefinition );
            myRequest = Objects.Clone( myRequest );

            var toolsHome = Path.GetDirectoryName( GetType().Assembly.Location );
            var workflowFsx = Path.Combine( toolsHome, "bits", "Workflow.fsx" );

            return Task<bool>.Run( () =>
                Try( "Workflow", Run( workflowFsx, "default" ), progress )
            );
        }

        private bool Try( string activity, Func<IProgress<string>, bool> action, IProgress<string> progress )
        {
            try
            {
                var success = action( progress );

                if( success )
                {
                    progress.Report( "--- " + activity.ToUpper() + " SUCCEEDED ---" );
                }
                else
                {
                    progress.Report( "--- " + activity.ToUpper() + " FAILED ---" );
                }

                return success;
            }
            catch( Exception ex )
            {
                progress.Report( "ERROR: " + ex.Message );
                progress.Report( "--- " + activity.ToUpper() + " FAILED ---" );
                return false;
            }
        }

        private Func<IProgress<string>, bool> Run( string script, string args )
        {
            return p =>
            {
                var executor = new FakeScriptExecutor( myDefinition, p );
                return executor.Execute( script, args.Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries ) );
            };
        }
    }
}
