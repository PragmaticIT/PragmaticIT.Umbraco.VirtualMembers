using Umbraco.Cms.Core.Packaging;

namespace SampleSeed
{
	public class ContentMigrationPlan : PackageMigrationPlan
	{
		public ContentMigrationPlan() : base("ContentSeed")
		{
		}

		protected override void DefinePlan()
		{
			To<ImportDataMigration>("content-seed-v1");
			To<FixTemplateAssignment>("content-seed-v2");
			To<RestrictPublicAccessMigration>("content-seed-v3");
		}
	}
}
