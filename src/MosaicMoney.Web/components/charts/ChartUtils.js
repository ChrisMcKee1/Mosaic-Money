export const aggregateByDay = (data, dateKey, valueKey) => {
  const aggregated = {};
  data.forEach(item => {
    const date = new Date(item[dateKey]);
    const key = date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    if (!aggregated[key]) {
      aggregated[key] = { name: key, value: 0, fullDate: new Date(date.getFullYear(), date.getMonth(), date.getDate()) };
    }
    aggregated[key].value += item[valueKey];
  });
  return Object.values(aggregated).sort((a, b) => a.fullDate - b.fullDate);
};

export const aggregateByWeek = (data, dateKey, valueKey) => {
  const aggregated = {};
  data.forEach(item => {
    const date = new Date(item[dateKey]);
    // Get start of week (Sunday)
    const startOfWeek = new Date(date);
    startOfWeek.setDate(date.getDate() - date.getDay());
    const key = startOfWeek.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    
    if (!aggregated[key]) {
      aggregated[key] = { name: key, value: 0, fullDate: startOfWeek };
    }
    aggregated[key].value += item[valueKey];
  });
  return Object.values(aggregated).sort((a, b) => a.fullDate - b.fullDate);
};

export const aggregateByMonth = (data, dateKey, valueKey) => {
  const aggregated = {};
  data.forEach(item => {
    const date = new Date(item[dateKey]);
    const key = date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
    const shortName = date.toLocaleDateString('en-US', { month: 'short' });
    
    if (!aggregated[key]) {
      aggregated[key] = { name: shortName, value: 0, fullDate: new Date(date.getFullYear(), date.getMonth(), 1) };
    }
    aggregated[key].value += item[valueKey];
  });
  return Object.values(aggregated).sort((a, b) => a.fullDate - b.fullDate);
};
