namespace PassManager.Configuration
{
    public static class AppVersion
    {
        public const string CURRENT_VERSION = "2.0.0";

        public const string GITHUB_OWNER = "TheKeProjects";
        public const string GITHUB_REPO = "PassManager";
        public const string GITHUB_BRANCH = "main";

        public const bool AUTO_UPDATE_ENABLED = true;

        public static string GetDisplayVersion()
        {
            return $"v{CURRENT_VERSION}";
        }

        public static string GetGitHubRepoUrl()
        {
            return $"https://github.com/{GITHUB_OWNER}/{GITHUB_REPO}";
        }
    }
}
