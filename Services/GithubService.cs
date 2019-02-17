using CheckStaging.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CheckStaging.Utils;
using System.Drawing;

namespace CheckStaging.Services
{
    public class GithubService
    {
        public const string GITHUB_CHANNEL = "Github";
        private string _githubLatestError = "";
        private bool Status { get => _githubLatestError.Length == 0; }
        public static readonly GithubService Instance = new GithubService();
        private GithubService()
        {
            if (!RemindService.Instance.HasChannel(GITHUB_CHANNEL))
            {
                _githubLatestError = "配置文件中尚未配置github channel";
                return;
            }
        }

        public void PassGithubWebhook(GithubWebhook incoming)
        {
            if (!this.Status) return;
            var type = incoming.GithubAction();

            switch (type)
            {
                case GithubPullRequestAction.SUBMITTED:
                    ReviewSubmit(incoming.pull_request.Value, incoming.review.Value);
                    break;
                case GithubPullRequestAction.REVIEW_REQUESTED:
                    RequestReview(incoming.pull_request.Value, incoming.sender, incoming.requested_reviewer.Value);
                    break;
                default:
                    break;
            }
        }

        private OutgoingAttachment _prToAttachment(GithubPullRequest pr)
        {
            return new OutgoingAttachment()
            {
                title = pr.title,
                url = pr.html_url,
                text = $"{pr.user.GetFriendlyName(false)} : Merge `{pr.head.@ref}` ({pr.head.sha.Substring(0, 6)}) -> `{pr.@base.@ref}`",
                color = Color.Orange.ToHtml()
            };
        }

        public void ReviewSubmit(GithubPullRequest pr, GithubReview review)
        {
            // review by pull requlest owner
            if (pr.user.login == review.user.login) return;
            var reviewResult = review.ReviewState();
            var reviewVerb = "Approve了";
            var reviewColor = Color.Green;
            var reviewer = review.user.GetFriendlyName(false);
            var prOwner = pr.user.GetFriendlyName();
            switch (reviewResult)
            {
                case GithubReviewStatus.CHANGES_REQUESTED:
                    reviewVerb = "觉得你需要修改";
                    reviewColor = Color.Red;
                    break;
                case GithubReviewStatus.COMMENTED:
                    reviewVerb = "评论了";
                    reviewColor = Color.LightBlue;
                    break;
                case GithubReviewStatus.APPROVED:
                default:
                    break;
            }
            var reviewAttachment = new OutgoingAttachment()
            {
                title = $"Review:{review.id} - {review.ReadableState()}",
                url = review.html_url,
                text = $"{review.body}",
                color = reviewColor.ToHtml()
            };
            RemindService.Instance.SendMessage(new Outgoing()
            {
                text = $"{prOwner} ，{reviewer} {reviewVerb}你的Pull Request:",
                attachments = new OutgoingAttachment[]
                {
                    reviewAttachment,
                    _prToAttachment(pr),
                },
            }, GITHUB_CHANNEL);
        }

        public void RequestReview(GithubPullRequest pr, GithubUser requester, GithubUser reviewer)
        {
            // Requester == Reviewer (Self review request)
            if (requester.login == reviewer.login)
            {
                return;
            }
            var requesterName = requester.GetFriendlyName(false);
            var reviewerName = reviewer.GetFriendlyName();
            var prOwnerName = pr.user.GetFriendlyName(false);
            var reviewStatement = $"他的Pull Request";
            if (prOwnerName != requesterName)
            {
                reviewStatement = $"{prOwnerName}的Pull Request";
            }
            RemindService.Instance.SendMessage(new Outgoing()
            {
                text = $"{reviewerName} ，{requesterName}想让你review {reviewStatement}:",
                attachments = new OutgoingAttachment[]
                {
                    _prToAttachment(pr),
                },
            }, GITHUB_CHANNEL);
        }
    }
}
