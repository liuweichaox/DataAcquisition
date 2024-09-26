var dataCollector = new DataCollector();

await dataCollector.StartCollectionTasks();

await Task.Delay(Timeout.Infinite);