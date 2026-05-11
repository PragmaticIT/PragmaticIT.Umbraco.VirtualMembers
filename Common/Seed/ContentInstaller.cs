using System.Xml.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
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
	public class FixTemplateAssignment : MigrationBase
	{
		private readonly IMigrationContext _context;
		private readonly IContentService _contentService;
		private readonly IFileService _fileService;

		// Maps content key (from XML) to the intended template alias.
		// The numeric template IDs in the XML are source-system specific and are
		// not portable, so we resolve templates by alias after import instead.
		private static readonly Dictionary<Guid, string> ContentKeyToTemplateAlias = new()
		{
			// home.xml
			[Guid.Parse("46c54847-aae8-49ae-bf43-d0b5b826d431")] = "home",
			[Guid.Parse("3626f723-782b-4275-9a52-be63f5857216")] = "content",
			// org-a.xml
			[Guid.Parse("b0321c35-e481-4aed-8e09-ba64ccc12480")] = "orgHome",
			[Guid.Parse("7d69c604-4be5-4db8-af13-6a8a626a6752")] = "content",
			[Guid.Parse("07af5397-4560-4f07-b625-f6de37c8820a")] = "content",
			// org-b.xml
			[Guid.Parse("eeba8b8a-301b-4ed3-9a34-50bfecee8e24")] = "orgHome",
			[Guid.Parse("79b7172f-b328-437e-b4c5-d241f3e25dc6")] = "content",
			[Guid.Parse("310145e0-d1b3-41fc-9f0f-2b88c7c04937")] = "content",
			// login.xml
			[Guid.Parse("b7717056-02d1-4eab-972b-784de7df23f4")] = "login",
			// logout.xml
			[Guid.Parse("10219e40-1e67-490c-a394-7bc24bad45d1")] = "logout",
			// access-denied.xml
			[Guid.Parse("d36afb30-271a-46b6-9353-482e3148b9e0")] = "accessDenied",
		};

		public FixTemplateAssignment(
			IMigrationContext context,
			IContentService contentService,
			IFileService fileService) : base(context)
		{
			_context = context;
			_contentService = contentService;
			_fileService = fileService;
		}

		protected override void Migrate()
		{
			var templateCache = new Dictionary<string, ITemplate?>();

			foreach (var (contentKey, templateAlias) in ContentKeyToTemplateAlias)
			{
				var content = _contentService.GetById(contentKey);
				if (content is null)
					continue;

				if (!templateCache.TryGetValue(templateAlias, out var template))
				{
					template = _fileService.GetTemplate(templateAlias);
					templateCache[templateAlias] = template;
				}

				if (template is null)
					continue;

				if (content.TemplateId == template.Id)
					continue;

				content.TemplateId = template.Id;
				_contentService.Save(content);
				_contentService.Publish(content, ["*"]);
			}

			_context.Complete();
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