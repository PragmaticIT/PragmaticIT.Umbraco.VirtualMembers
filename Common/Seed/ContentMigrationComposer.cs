using Umbraco.Cms.Core.Composing;

namespace SampleSeed
{
	public class ContentMigrationComposer : IComposer
	{
		public void Compose(IUmbracoBuilder builder)
		{
			builder.PackageMigrationPlans().Add<ContentMigrationPlan>();
		}
	}
}
