using CheckStaging.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CheckStaging.Models
{
    public struct GithubUser
    {
        public int id { get; set; }
        public string login { get; set; }
        public string GetFriendlyName(bool withAt = true) => ChannelService.Instance.ToFriendlyName("github", login, withAt);
        public string avatar_url { get; set; }
        public string type { get; set; }
    }

    public struct GithubBranch
    {
        /// <summary>
        /// full name of remote branch
        /// </summary>
        public string label { get; set; }
        /// <summary>
        /// branch name
        /// </summary>
        public string @ref { get; set; }
        /// <summary>
        /// branch sha
        /// </summary>
        public string sha { get; set; }
        /// <summary>
        /// who create this branch
        /// </summary>
        public GithubUser user { get; set; }

    }

    public enum GithubReviewStatus
    {
        APPROVED,
        CHANGES_REQUESTED,
        COMMENTED,
    }

    public enum GithubPullRequestStatus
    {
        OPEN,
        CLOSE,
    }

    public struct GithubReview
    {
        public int id { get; set; }
        public string commit { get; set; }
        public DateTime submitted_at { get; set; }
        /// <summary>
        /// Review status, "approved", "changes_requested", "commented"
        /// </summary>
        public string state { get; set; }
        public GithubReviewStatus ReviewState() => Enum.Parse<GithubReviewStatus>(this.state, true);
        public string ReadableState()
        {
            var state = ReviewState();
            switch (state)
            {
                case GithubReviewStatus.APPROVED:
                    return "Approved";
                case GithubReviewStatus.CHANGES_REQUESTED:
                    return "Changes Requested";
                case GithubReviewStatus.COMMENTED:
                    return "Commented";
                default:
                    return "Unknown State";
            }
        }
        /// <summary>
        /// Review comment
        /// </summary>
        public string body { get; set; }
        /// <summary>
        /// review url
        /// </summary>
        public string html_url { get; set; }
        /// <summary>
        /// who submit this review
        /// </summary>
        public GithubUser user { get; set; }
    }

    public struct GithubPullRequest
    {
        public int id { get; set; }
        /// <summary>
        /// who create this pull request
        /// </summary>
        public GithubUser user { get; set; }
        /// <summary>
        /// Pull request title
        /// </summary>
        public string title { get; set; }
        /// <summary>
        /// Pull request #number
        /// </summary>
        public int number { get; set; }
        public string html_url { get; set; }
        public GithubUser? assignee { get; set; }
        public GithubUser[] assignees { get; set; }
        public GithubUser[] requested_reviewers { get; set; }
        /// <summary>
        /// pull request merge base
        /// </summary>
        public GithubBranch @base {get; set;}
        /// <summary>
        /// which branch want merge to base
        /// </summary>
        public GithubBranch head { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum GithubPullRequestAction
    {
        SUBMITTED,
        REVIEW_REQUESTED,
        REVIEW_REQUEST_REMOVED,
    }

    public struct GithubWebhook
    {
        public string action { get; set; }
        public GithubPullRequestAction GithubAction() => Enum.Parse<GithubPullRequestAction>(this.action, true);
        public GithubUser sender { get; set; }
        public GithubUser? requested_reviewer { get; set; }
        public GithubReview? review { get; set; }
        public GithubPullRequest? pull_request { get; set; }
    }
}
