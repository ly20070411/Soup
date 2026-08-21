using System;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    [Serializable]
    public sealed class ProcessJobArtPreset
    {
        [SerializeField] private string jobId;
        [SerializeField] private SpriteRenderer template;

        public string JobId => jobId;
        public SpriteRenderer Template => template;

        public bool Matches(JobItem job) =>
            job != null && !string.IsNullOrEmpty(jobId) &&
            string.Equals(job.Id, jobId, StringComparison.Ordinal);
    }
}
