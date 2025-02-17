﻿using System;
using FreeSql;
using FreeSql.DataAnnotations;

namespace AntFlowCore.Entity
{
    /// <summary>
    /// Represents the process category.
    /// </summary>
    [Table(Name = "bpm_process_category")]
    public class BpmProcessCategory
    {
        /// <summary>
        /// Auto-increment ID.
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// Process type name.
        /// </summary>
        [Column(Name = "process_type_name")]
        public string ProcessTypeName { get; set; }

        /// <summary>
        /// Deletion state (0 for normal, 1 for deleted).
        /// </summary>
        [Column(Name = "is_del")]
        public int IsDel { get; set; }

        /// <summary>
        /// State of the category.
        /// </summary>
        [Column(Name = "state")]
        public int State { get; set; }

        /// <summary>
        /// Sort order.
        /// </summary>
        public int Sort { get; set; }

        /// <summary>
        /// Indicates if it is for the app (0 for no, 1 for yes).
        /// </summary>
        [Column(Name = "is_app")]
        public int IsApp { get; set; }

        /// <summary>
        /// Entrance URL.
        /// </summary>
        [Column(Name = "entrance")]
        public string Entrance { get; set; }
    }
}