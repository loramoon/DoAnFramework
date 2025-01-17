﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnFramework.src.Models
{
    public class Participant : AuditInfo
    {
        public Participant()
        {
        }

        public Participant(int contestId, string userId, bool isOfficial)
        {
            this.ContestId = contestId;
            this.UserId = userId;
            this.IsOfficial = isOfficial;
        }

        [Key]
        public int Id { get; set; }

        public int ContestId { get; set; }

        public string UserId { get; set; }

        public DateTime? ParticipationStartTime { get; set; }

        public DateTime? ParticipationEndTime { get; set; }

        [Index]
        public bool IsOfficial { get; set; }

        public bool IsInvalidated { get; set; }

        public virtual Contest Contest { get; set; }

        public virtual UserProfile User { get; set; }

        public virtual ICollection<SubmissionInput> Submissions { get; set; } = new HashSet<SubmissionInput>();

        public virtual ICollection<ParticipantAnswer> Answers { get; set; } = new HashSet<ParticipantAnswer>();

        public virtual ICollection<ParticipantScore> Scores { get; set; } = new HashSet<ParticipantScore>();

        public virtual ICollection<Problem> Problems { get; set; } = new HashSet<Problem>();
    }
}
