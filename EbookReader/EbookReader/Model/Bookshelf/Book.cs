﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EbookReader.Model.Bookshelf {
    public class Book {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string Cover { get; set; }
        public Position Position { get; set; }
        public List<Bookmark> Bookmarks { get; set; }

        public Book() {
            Position = new Position();
            Bookmarks = new List<Bookmark>();
        }

    }
}
