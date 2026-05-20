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
			To<ImportDataMigration>("content-seed-step1");
			To<FixTemplateAssignment>("content-seed-step2");
			To<RestrictPublicAccessMigration>("content-seed-step3");
			To<InsertCreatedPackagesMigration>("content-seed-step4");
		}
	}
}
