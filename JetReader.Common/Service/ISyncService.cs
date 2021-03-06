﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetReader.Books;
using JetReader.Model.Sync;

namespace JetReader.Service
{
    public interface ISyncService {
        void SaveProgress(string bookId, Position position);
        Task<Progress> LoadProgress(string bookId);
        void DeleteBook(string bookId);
        Task BackupDatabase();
        Task<List<string>> GetDatabaseRestorationList();
        Task<bool> RestoreBackup(string path);
        void SaveBookmark(string bookId, Books.Bookmark bookmark);
        void SynchronizeBookmarks(BookInfo book);
    }
}
