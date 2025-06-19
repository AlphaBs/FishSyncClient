using FishSyncClient.PathMatchers;

namespace FishSyncClientTest;

public class RulePathMatcherTests
{
    [Fact]
    public void match_true_when_no_rules_are_added()
    {
        // Given
        var sut = new RulePathMatcher();

        // When
        var result = sut.Match("any/path/will/do.txt");

        // Then
        Assert.True(result, "규칙이 없으면 기본적으로 true (포함) 여야 합니다.");
    }

    [Fact]
    public void match_false_when_path_matches_glob_exclude_rule()
    {
        // Given
        var sut = new RulePathMatcher();
        sut.AddExcludeRule(new GlobPathMatcher("*.log")); // .log 확장자를 가진 모든 파일 제외

        // When
        var result = sut.Match("system.log");

        // Then
        Assert.False(result, ".log 파일은 제외 규칙에 의해 false여야 합니다.");
    }

    [Fact]
    public void match_true_when_path_does_not_match_any_rule()
    {
        // Given
        var sut = new RulePathMatcher();
        sut.AddExcludeRule(new GlobPathMatcher("**/node_modules/**")); // node_modules 폴더 제외
        sut.AddExcludeRule(new GlobPathMatcher("*.tmp"));           // 임시 파일 제외

        // When
        var result = sut.Match("src/components/button.tsx"); // 규칙에 해당하지 않는 경로

        // Then
        Assert.True(result, "어떤 규칙과도 일치하지 않으면 기본적으로 true여야 합니다.");
    }

    [Fact]
    public void match_true_when_specific_include_rule_comes_before_general_exclude()
    {
        // Given: 더 구체적인 포함 규칙이 먼저 오는 경우
        var sut = new RulePathMatcher();
        sut.AddIncludeRule(new GlobPathMatcher("src/assets/important.log"));
        sut.AddExcludeRule(new GlobPathMatcher("**/*.log"));

        // When: 포함 규칙에 해당하는 경로를 테스트
        var result = sut.Match("src/assets/important.log");

        // Then
        Assert.True(result, "포함 규칙이 먼저 적용되어 true여야 합니다.");
    }

    [Fact]
    public void match_false_when_path_matches_later_general_exclude_rule()
    {
        // Given: 포함 규칙과 제외 규칙이 순서대로 있는 경우
        var sut = new RulePathMatcher();
        sut.AddIncludeRule(new GlobPathMatcher("src/assets/important.log"));
        sut.AddExcludeRule(new GlobPathMatcher("**/*.log"));

        // When: 포함 규칙이 아닌 제외 규칙에 해당하는 경로를 테스트
        var result = sut.Match("logs/another.log");

        // Then
        Assert.False(result, "포함 규칙에는 맞지 않고, 제외 규칙에 맞으므로 false여야 합니다.");
    }

    [Fact]
    public void match_false_when_general_exclude_rule_comes_before_specific_include()
    {
        // Given: 일반적인 제외 규칙이 먼저 오는 경우
        var sut = new RulePathMatcher();
        sut.AddExcludeRule(new GlobPathMatcher("**/*.log"));
        sut.AddIncludeRule(new GlobPathMatcher("src/assets/important.log"));

        // When: 경로가 첫 번째 규칙인 제외 규칙과 바로 매칭됨
        var result = sut.Match("src/assets/important.log");

        // Then
        Assert.False(result, "더 먼저 추가된 일반 제외 규칙이 적용되어 false여야 합니다.");
    }

    [Theory]
    [InlineData(".env", false)]
    [InlineData("project/node_modules/library/index.js", false)]
    [InlineData("project/dist/app.bundle.js", false)]
    [InlineData("project/dist/config.json", true)]
    [InlineData("project/src/index.ts", true)]
    [InlineData("project/src/config.json", true)]
    [InlineData("project/src/start.tmp", true)]
    public void match_returns_correct_result_for_various_paths_in_a_complex_scenario(string path, bool expectedResult)
    {
        // Given
        var sut = new RulePathMatcher()
            .AddExcludeRule(new GlobPathMatcher("**/node_modules/**"))
            .AddExcludeRule(new GlobPathMatcher("**/.git/**"))
            .AddIncludeRule(new GlobPathMatcher("**/config.json"))
            .AddExcludeRule(new GlobPathMatcher("**/dist/**"))
            .AddExcludeRule(new GlobPathMatcher("*.tmp"))
            .AddExcludeRule(new GlobPathMatcher(".env"));

        // When
        var result = sut.Match(path);

        // Then
        Assert.Equal(expectedResult, result);
    }
}
