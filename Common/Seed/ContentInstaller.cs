using Microsoft.Extensions.Options;
using System.Xml.Linq;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Packaging;

namespace SampleSeed
{
	public class ImportDataMigration
		: MigrationBase
	{
		private readonly IMigrationContext _context;
		private readonly IPackagingService _packagingService;
		private readonly IContentService _contentService;
		private string[] resourceNames = {
			"home",
			"org-a",
			"org-b",
			"login",
			"logout",
			"access-denied"
		};

		public ImportDataMigration(IMigrationContext context,
		IPackagingService packagingService,
		IContentService contentService) : base(context)
		{
			_context = context;
			_packagingService = packagingService;
			_contentService = contentService;
		}
		protected override void Migrate()
		{
			try
			{
				var assemblyResourceNames = GetType().Assembly.GetManifestResourceNames();
				var resources = resourceNames.Select(rn =>
						assemblyResourceNames.First(arn =>
							arn.EndsWith($".{rn}.xml", StringComparison.InvariantCultureIgnoreCase)))
						.ToArray();
				foreach (var resource in resources)
				{
					XDocument? packageDocument = null;
					using (var stream = GetType().Assembly.GetManifestResourceStream(resource)
						?? throw new InvalidOperationException($"Embedded resource '{resource}' not found."))
					{
						packageDocument = XDocument.Load(stream);
					}
					var summary = _packagingService.InstallCompiledPackageData(packageDocument);
					foreach (var node in summary.ContentInstalled)
					{
							_contentService.Publish(node, ["*"]);
					}
				}
				_context.Complete();
			}
			catch (Exception ex)
			{
				throw;
			}
		}
	}
	public class RestrictPublicAccessMigration : MigrationBase
	{
		private readonly IMigrationContext _context;
		private readonly IContentService _contentService;
		private readonly IMemberGroupService _memberGroupService;
		private readonly IPublicAccessService _publicAccessService;

		public RestrictPublicAccessMigration(
			IMigrationContext context,
			IContentService contentService,
			IMemberGroupService memberGroupService,
			IPublicAccessService publicAccessService) : base(context)
		{
			_context = context;
			_contentService = contentService;
			_memberGroupService = memberGroupService;
			_publicAccessService = publicAccessService;
		}

		protected override void Migrate() =>
			RestrictPublicAccessAsync().GetAwaiter().GetResult();

		private async Task RestrictPublicAccessAsync()
		{
			var loginPage = FindContent("login");
			if (loginPage == null)
				throw new InvalidOperationException("Login page not found. Ensure the 'login.xml' resource is correctly embedded and imported.");

			var accessDeniedPage = FindContent("access denied");
			if (accessDeniedPage == null)
				throw new InvalidOperationException("Access Denied page not found. Ensure the 'access-denied.xml' resource is correctly embedded and imported.");


			foreach (var content in _contentService.GetRootContent()
				.Where(x => x.Name.ToLowerInvariant() == "org-a"
						 || x.Name.ToLowerInvariant() == "org-b"))
			{
				var grp = await _memberGroupService.GetByNameAsync(content.Name);
				if (grp is null)
				{
					var attempt = await _memberGroupService.CreateAsync(new MemberGroup { Name = content.Name });
					grp = attempt.Result!;
				}

				await _publicAccessService.CreateAsync(new PublicAccessEntrySlim
				{
					ContentId = content.Key,
					LoginPageId = loginPage!.Key,
					ErrorPageId = accessDeniedPage!.Key,
					MemberGroupNames = [grp.Name!]
				});
			}

			_context.Complete();
		}

		private IContent? FindContent(string name) =>
			_contentService.GetRootContent()
				.FirstOrDefault(x => x.Name?.ToLowerInvariant() == name);
	}
}