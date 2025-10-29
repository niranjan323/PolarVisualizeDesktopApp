window.polarChart = window.polarChart || {};

window.polarChart.initialized = false;

window.polarChart.renderChart = function (containerId, chartDataOrJson) {
    console.log('polarChart.renderChart called');

    const container = document.getElementById(containerId);
    if (!container) {
        console.error('Chart container not found:', containerId);
        return;
    }

    if (typeof echarts === 'undefined') {
        console.error('ECharts library not loaded!');
        return;
    }

    if (window.polarChart.instance) {
        window.polarChart.instance.dispose();
    }

    let chartData = chartDataOrJson;
    if (typeof chartDataOrJson === 'string') {
        try {
            chartData = JSON.parse(chartDataOrJson);
        } catch (e) {
            console.error('Failed to parse chart data JSON:', e);
            return;
        }
    }

    const chart = echarts.init(container);
    window.polarChart.instance = chart;

    // Parse data
    const angles = chartData.angles || [];
    const radii = chartData.radii || [];
    const matrix = chartData.matrix || [];
    const maxRoll = chartData.maxRoll || 30;
    const mode = chartData.mode || 'continuous';
    const vesselHeading = chartData.vesselHeading || 0;
    const vesselSpeed = chartData.vesselSpeed || 0;
    const startAngle = chartData.startAngle || 90;

    console.log('Chart data parsed:', {
        angleCount: angles.length,
        radiiCount: radii.length,
        mode,
        maxRoll
    });

    // Prepare data for ECharts
    const chartDataPoints = [];
    for (let i = 0; i < radii.length; i++) {
        for (let j = 0; j < angles.length; j++) {
            if (matrix[i] && matrix[i][j] !== undefined) {
                chartDataPoints.push([angles[j], radii[i], matrix[i][j]]);
            }
        }
    }

    // Color scale based on mode
    let visualMap;
    if (mode === 'trafficlight') {
        visualMap = {
            type: 'piecewise',
            min: 0,
            max: Math.max(...matrix.flat()),
            calculable: false,
            realtime: false,
            splitNumber: 3,
            pieces: [
                { min: 0, max: maxRoll - 5, color: '#2ecc71', label: 'Safe (0-' + (maxRoll - 5) + '°)' },
                { min: maxRoll - 5, max: maxRoll, color: '#f39c12', label: 'Caution (' + (maxRoll - 5) + '-' + maxRoll + '°)' },
                { min: maxRoll, color: '#e74c3c', label: 'Danger (>' + maxRoll + '°)' }
            ],
            orient: 'vertical',
            left: 'left',
            top: 'center',
            textStyle: { color: '#fff' }
        };
    } else {
        visualMap = {
            type: 'continuous',
            min: 0,
            max: maxRoll,
            calculable: true,
            realtime: true,
            inRange: {
                color: [
                    '#313695', '#4575b4', '#74add1', '#abd9e9', '#e0f3f8',
                    '#ffffbf', '#fee090', '#fdae61', '#f46d43', '#d73027', '#a50026'
                ]
            },
            orient: 'vertical',
            left: 'left',
            top: 'center',
            textStyle: { color: '#fff' }
        };
    }

    // Chart options
    const option = {
        backgroundColor: '#1a1a1a',
        title: {
            text: 'Roll Angle (deg)',
            left: 'center',
            top: 20,
            textStyle: { color: '#fff', fontSize: 16 }
        },
        tooltip: {
            trigger: 'item',
            formatter: function (params) {
                if (params.componentType === 'series' && params.seriesType === 'scatter') {
                    return `<b>Heading:</b> ${params.value[0].toFixed(1)}°<br/>
                            <b>Speed:</b> ${params.value[1].toFixed(1)} kn<br/>
                            <b>Roll:</b> ${params.value[2].toFixed(2)}°`;
                }
                return '';
            }
        },
        visualMap: visualMap,
        polar: {
            center: ['50%', '55%'],
            radius: '60%'
        },
        angleAxis: {
            type: 'value',
            startAngle: startAngle,
            min: 0,
            max: 360,
            interval: 30,
            splitLine: { lineStyle: { color: '#444' } },
            axisLabel: {
                formatter: '{value}°',
                color: '#fff'
            },
            axisLine: { lineStyle: { color: '#666' } }
        },
        radiusAxis: {
            type: 'value',
            min: 0,
            max: Math.max(...radii) * 1.1,
            splitLine: { lineStyle: { color: '#444' } },
            axisLabel: {
                formatter: '{value} kn',
                color: '#fff'
            },
            axisLine: { lineStyle: { color: '#666' } }
        },
        series: [
            {
                name: 'Roll Response',
                type: 'scatter',
                coordinateSystem: 'polar',
                symbolSize: function (val) {
                    return Math.max(8, 400 / chartDataPoints.length);
                },
                data: chartDataPoints,
                itemStyle: {
                    opacity: 0.85
                }
            }
        ]
    };

    // Add vessel position marker if specified
    if (vesselSpeed > 0 && vesselHeading >= 0) {
        option.series.push({
            name: 'Vessel Position',
            type: 'scatter',
            coordinateSystem: 'polar',
            symbolSize: 20,
            data: [[vesselHeading, vesselSpeed, 0]],
            itemStyle: {
                color: '#fff',
                borderColor: '#0066ff',
                borderWidth: 3
            },
            label: {
                show: true,
                formatter: 'Vessel',
                position: 'top',
                color: '#fff'
            }
        });
    }

    // Set option and render
    chart.setOption(option);

    // Handle window resize
    window.addEventListener('resize', function () {
        if (window.polarChart.instance) {
            window.polarChart.instance.resize();
        }
    });
};

window.polarChart.exportImage = function () {
    if (window.polarChart.instance) {
        return window.polarChart.instance.getDataURL({
            type: 'png',
            pixelRatio: 2,
            backgroundColor: '#1a1a1a'
        });
    }
    return null;
};


window.polarChart.dispose = function () {
    if (window.polarChart.instance) {
        window.polarChart.instance.dispose();
        window.polarChart.instance = null;
    }
};