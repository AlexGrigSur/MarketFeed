using MarketFeed.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketFeed.UnitTests.Fakes;

internal sealed class FakeProcessorMetrics : IProcessorMetrics
{
    public void TicksPersisted(int count) { }
    public void BatchSaved() { }
    public void BatchDropped() { }
    public void SetChannelDepth(int depth) { }
    public void RecordSaveDuration(TimeSpan elapsed) { }
    public void RecordBatchSize(int size) { }
}
