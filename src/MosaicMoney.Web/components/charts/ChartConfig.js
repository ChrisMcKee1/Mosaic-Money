const resolveTheme = (theme) => (theme === "light" ? "light" : "dark");

export const getBaseChartOptions = (theme = "dark") => {
  const mode = resolveTheme(theme);
  const isDark = mode === "dark";
  const textColor = isDark ? "#9ca3af" : "#6b7280";
  const borderColor = isDark ? "#374151" : "#e5e7eb";

  return {
    chart: {
      toolbar: { show: false },
      background: "transparent",
      fontFamily: "inherit",
      animations: {
        enabled: true,
        easing: "easeinout",
        speed: 800,
        animateGradually: {
          enabled: true,
          delay: 150,
        },
        dynamicAnimation: {
          enabled: true,
          speed: 350,
        },
      },
    },
    theme: {
      mode,
    },
    grid: {
      borderColor: borderColor,
      strokeDashArray: 3,
      xaxis: { lines: { show: false } },
      yaxis: { lines: { show: true } },
      padding: { top: 0, right: 0, bottom: 0, left: 10 },
    },
    dataLabels: { enabled: false },
    stroke: {
      curve: "smooth",
      width: 2,
    },
    xaxis: {
      labels: {
        style: { colors: textColor, fontSize: "11px", fontWeight: 500 },
        offsetY: 5,
      },
      axisBorder: { show: false },
      axisTicks: { show: false },
      tooltip: { enabled: false }
    },
    yaxis: {
      labels: {
        style: { colors: textColor, fontSize: "11px", fontWeight: 500 },
        formatter: (value) => `$${value.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`,
      },
    },
    tooltip: {
      theme: mode,
      style: { fontSize: "12px" },
      y: {
        formatter: (value) => `$${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
      },
      marker: { show: false },
    },
    legend: { show: false },
  };
};

export const getSparklineOptions = (isPositive, theme = "dark") => {
  const mode = resolveTheme(theme);
  const color = isPositive ? "var(--color-positive)" : "var(--color-negative)";
  
  return {
    chart: {
      type: "line",
      sparkline: { enabled: true },
      animations: { enabled: false },
    },
    theme: { mode },
    stroke: {
      curve: "smooth",
      width: 2,
    },
    colors: [color],
    tooltip: {
      fixed: { enabled: false },
      x: { show: false },
      y: {
        title: { formatter: () => "" },
      },
      marker: { show: false },
    },
  };
};

export const getDonutOptions = (theme = "dark") => {
  const mode = resolveTheme(theme);
  
  return {
    chart: {
      type: "donut",
      background: "transparent",
    },
    theme: {
      mode,
    },
    stroke: {
      show: true,
      colors: ['var(--color-surface)'],
      width: 2,
    },
    dataLabels: {
      enabled: false,
    },
    plotOptions: {
      pie: {
        donut: {
          size: '75%',
          labels: {
            show: false,
          },
        },
        expandOnClick: false,
      },
    },
    legend: {
      show: false,
    },
    tooltip: {
      theme: mode,
      y: {
        formatter: (value) => `$${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
      },
    },
  };
};
