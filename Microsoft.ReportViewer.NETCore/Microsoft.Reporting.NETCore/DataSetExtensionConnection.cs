using Microsoft.ReportingServices.DataExtensions;
using Microsoft.ReportingServices.DataProcessing;
using Microsoft.ReportingServices.Diagnostics;
using Microsoft.ReportingServices.ReportProcessing;
using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Reporting.NETCore
{
	internal class DataSetExtensionConnection : IProcessingDataExtensionConnection
	{
		private LocalDataRetrievalFromDataSet.GetSubReportDataSetCallback m_subreportCallback;

		private readonly IEnumerable m_rootDataSources;

		private IEnumerable m_dataSources;

		// Tracks the data sources resolved for whichever subreport instance was most recently
		// entered via DataSetRetrieveForReportInstance, independent of m_dataSources, so that
		// EnsureCorrectDataSources can re-select the right one on demand without re-invoking
		// the subreport callback (see EnsureCorrectDataSources below).
		private IEnumerable m_lastSubreportDataSources;

		public bool MustResolveSharedDataSources => false;

		public DataSetExtensionConnection(LocalDataRetrievalFromDataSet.GetSubReportDataSetCallback subreportCallback, IEnumerable dataSources)
		{
			m_subreportCallback = subreportCallback;
            m_rootDataSources = dataSources;
			m_dataSources = dataSources;
		}

		public void DataSetRetrieveForReportInstance(ICatalogItemContext itemContext, ParameterInfoCollection reportParameters)
		{
			IEnumerable enumerable = m_subreportCallback((PreviewItemContext)itemContext, reportParameters);
			IEnumerable enumerable2 = new DataSourceCollectionWrapper((ReportDataSourceCollection)enumerable);
			m_dataSources = enumerable2;
			m_lastSubreportDataSources = enumerable2;
		}


		// A single DataSetExtensionConnection instance is shared for the entire render (root and
		// every subreport instance). On-demand processing interleaves root and subreport dataset
		// execution: a subreport can be triggered mid-way through the root's own dataset
		// processing (as the report body/group tree is walked), and there is no matching
		// "leaving subreport scope" callback to restore state when control returns to the root.
		// So rather than relying on whichever scope last called DataSetRetrieveForReportInstance/
		// RestoreRootDataSources, callers must re-assert which data sources apply immediately
		// before actually opening a connection for a given dataset (see RuntimeDataSource.OpenConnection).
		public void EnsureCorrectDataSources(bool inSubreport)
		{
			IEnumerable enumerable = inSubreport ? (m_lastSubreportDataSources ?? m_dataSources) : m_rootDataSources;
			m_dataSources = enumerable;
		}

		public void HandleImpersonation(IProcessingDataSource dataSource, DataSourceInfo dataSourceInfo, string datasetName, IDbConnection connection, System.Action afterImpersonationAction)
		{
			afterImpersonationAction?.Invoke();
		}

		public IDbConnection OpenDataSourceExtensionConnection(IProcessingDataSource dataSource, string connectionString, DataSourceInfo dataSourceInfo, string datasetName)
		{
			return new DataSetProcessingExtension(m_dataSources, datasetName);
		}

		public void CloseConnection(IDbConnection connection, IProcessingDataSource dataSourceObj, DataSourceInfo dataSourceInfo)
		{
			CloseConnectionWithoutPool(connection);
		}

		public void CloseConnectionWithoutPool(IDbConnection connection)
		{
			connection.Close();
		}
	}
}
