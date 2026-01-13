using System.Collections.Generic;

namespace LagoVista.AI.Models.AuthoritativeAnswers
{
    public enum AuthoritativeAnswerLookupStatus
    {
        Answered,
        NotFound,
        Conflict
    }

    public class AuthoritativeAnswerLookupMatch
    {
        public string AqId { get; set; }
        public string Answer { get; set; }
        public string SourceRef { get; set; }
        public string Confidence { get; set; }
    }

    public class AuthoritativeAnswerLookupResult
    {
        public AuthoritativeAnswerLookupStatus Status { get; set; }
        public string Answer { get; set; }
        public string SourceRef { get; set; }
        public string Confidence { get; set; }

        /// <summary>
        /// Populated when Status == Conflict to allow the caller to disambiguate.
        /// </summary>
        public List<AuthoritativeAnswerLookupMatch> Conflicts { get; set; } = new List<AuthoritativeAnswerLookupMatch>();
    }
}
