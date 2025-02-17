﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YearInReview.Infrastructure.Services;
using YearInReview.Model.Reports._1970;
using YearInReview.Model.Reports.Persistence;

namespace YearInReview.Model.Reports
{
	public class ReportManager
	{
		private readonly IReportPersistence _reportPersistence;
		private readonly IReportGenerator _reportGenerator;
		private readonly IDateTimeProvider _dateTimeProvider;

		private Dictionary<Guid, PersistedReport> _reportCache;

		public ReportManager(
			IReportPersistence reportPersistence,
			IReportGenerator reportGenerator,
			IDateTimeProvider dateTimeProvider)
		{
			_reportPersistence = reportPersistence;
			_reportGenerator = reportGenerator;
			_dateTimeProvider = dateTimeProvider;
		}

		// TODO Don't forget. There's probably gonna be race condition between report manager and UI.
		public async Task Init()
		{
			var reports = _reportPersistence.PreLoadAllReports();
			if (!reports.Any())
			{
				reports = await GenerateAll();
			}
			else
			{
				reports = await GenerateNewYears(reports);
			}

			_reportCache = reports.ToDictionary(x => x.Id, x => x);
		}

		public Report1970 GetReport(Guid id)
		{
			if (!_reportCache.TryGetValue(id, out var persistedReport))
			{
				throw new Exception($"Report {id} not persisted in cache.");
			}

			var report = _reportPersistence.LoadReport(persistedReport.FilePath);
			return report;
		}

		private async Task<IReadOnlyCollection<PersistedReport>> GenerateAll()
		{
			var generatedReports = await _reportGenerator.GenerateAllYears();
			foreach (var report in generatedReports)
			{
				_reportPersistence.SaveReport(report);
			}

			var reports = _reportPersistence.PreLoadAllReports();
			return reports;
		}

		private async Task<IReadOnlyCollection<PersistedReport>> GenerateNewYears(
			IReadOnlyCollection<PersistedReport> reports)
		{
			var mostRecentOwnReport = reports.Where(x => x.IsOwn).OrderByDescending(x => x.Year).FirstOrDefault();
			if (mostRecentOwnReport?.Year < _dateTimeProvider.GetNow().Year - 1)
			{
				var yearsToGenerate = new List<int>();
				for (var i = mostRecentOwnReport.Year + 1; i < _dateTimeProvider.GetNow().Year; i++)
				{
					yearsToGenerate.Add(i);
				}

				var generatedReports = await Task.WhenAll(yearsToGenerate.Select(year => _reportGenerator.Generate(year)));
				foreach (var report in generatedReports)
				{
					_reportPersistence.SaveReport(report);
				}

				reports = _reportPersistence.PreLoadAllReports();
			}

			return reports;
		}

		public IReadOnlyCollection<PersistedReport> GetAllPreLoadedReports()
		{
			return _reportCache.Values.ToList();
		}
	}
}