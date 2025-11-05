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

    // Dispose existing chart instance
    if (window.polarChart.instance) {
        window.polarChart.instance.dispose();
        window.polarChart.instance = null;
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
    const directionMode = chartData.directionMode || 'northup';
    const vesselHeading = chartData.vesselHeading || 0;
    const vesselSpeed = chartData.vesselSpeed || 0;
    const waveDirection = chartData.waveDirection || 0;
    const startAngle = chartData.startAngle || 90;

    console.log('Chart data parsed:', {
        angleCount: angles.length,
        radiiCount: radii.length,
        mode,
        maxRoll,
        directionMode
    });

    // Create interpolated grid for smooth contours
    // We need much denser grid to create filled contour appearance
    const numAngles = 360; // One point per degree for smooth contours
    const angleStep = 360 / numAngles;

    // Create dense angle array
    const denseAngles = [];
    for (let i = 0; i < numAngles; i++) {
        denseAngles.push(i * angleStep);
    }

    // Interpolate radii for smoother radial transitions
    const denseRadii = [];
    const radialSteps = radii.length * 3; // 3x density
    const minRadius = Math.min(...radii);
    const maxRadius = Math.max(...radii);
    const radialStep = (maxRadius - minRadius) / (radialSteps - 1);

    for (let i = 0; i < radialSteps; i++) {
        denseRadii.push(minRadius + i * radialStep);
    }

    // Create interpolation function
    function interpolateRoll(targetAngle, targetRadius) {
        // Find nearest angle indices
        let angleIdx1 = 0;
        let angleIdx2 = 0;
        let minAngleDiff = 360;

        for (let i = 0; i < angles.length; i++) {
            let diff = Math.abs(angles[i] - targetAngle);
            if (diff > 180) diff = 360 - diff; // Handle wrap-around

            if (diff < minAngleDiff) {
                minAngleDiff = diff;
                angleIdx1 = i;
            }
        }

        // Find second nearest angle
        minAngleDiff = 360;
        for (let i = 0; i < angles.length; i++) {
            if (i === angleIdx1) continue;
            let diff = Math.abs(angles[i] - targetAngle);
            if (diff > 180) diff = 360 - diff;

            if (diff < minAngleDiff) {
                minAngleDiff = diff;
                angleIdx2 = i;
            }
        }

        // Find nearest radius indices
        let radiusIdx1 = 0;
        let radiusIdx2 = 0;

        for (let i = 0; i < radii.length - 1; i++) {
            if (targetRadius >= radii[i] && targetRadius <= radii[i + 1]) {
                radiusIdx1 = i;
                radiusIdx2 = i + 1;
                break;
            }
        }

        // If outside range, use nearest
        if (targetRadius < radii[0]) {
            radiusIdx1 = radiusIdx2 = 0;
        } else if (targetRadius > radii[radii.length - 1]) {
            radiusIdx1 = radiusIdx2 = radii.length - 1;
        }

        // Bilinear interpolation
        const r1 = radii[radiusIdx1];
        const r2 = radii[radiusIdx2];
        const rFactor = r2 > r1 ? (targetRadius - r1) / (r2 - r1) : 0;

        const v11 = matrix[radiusIdx1] && matrix[radiusIdx1][angleIdx1] !== undefined ? matrix[radiusIdx1][angleIdx1] : 0;
        const v12 = matrix[radiusIdx1] && matrix[radiusIdx1][angleIdx2] !== undefined ? matrix[radiusIdx1][angleIdx2] : 0;
        const v21 = matrix[radiusIdx2] && matrix[radiusIdx2][angleIdx1] !== undefined ? matrix[radiusIdx2][angleIdx1] : 0;
        const v22 = matrix[radiusIdx2] && matrix[radiusIdx2][angleIdx2] !== undefined ? matrix[radiusIdx2][angleIdx2] : 0;

        const v1 = v11 * (1 - rFactor) + v21 * rFactor;
        const v2 = v12 * (1 - rFactor) + v22 * rFactor;

        // Average between two nearest angles
        return (v1 + v2) / 2;
    }

    // Generate dense heatmap data
    const heatmapData = [];

    for (let i = 0; i < denseRadii.length; i++) {
        for (let j = 0; j < denseAngles.length; j++) {
            const rollValue = interpolateRoll(denseAngles[j], denseRadii[i]);
            // Format: [angle, radius, value]
            heatmapData.push([denseAngles[j], denseRadii[i], rollValue]);
        }
    }

    // Color scale based on mode
    let visualMap;
    if (mode === 'trafficlight') {
        const greenMax = maxRoll - 5;
        const yellowMin = maxRoll - 5;
        const yellowMax = maxRoll;
        const redMin = maxRoll;

        visualMap = {
            type: 'piecewise',
            min: 0,
            max: Math.max(...matrix.flat()),
            calculable: false,
            realtime: false,
            pieces: [
                {
                    min: 0,
                    max: greenMax,
                    color: '#2ecc71',
                    label: `Safe (0-${greenMax.toFixed(0)}\u00B0)`
                },
                {
                    min: yellowMin,
                    max: yellowMax,
                    color: '#f39c12',
                    label: `Caution (${yellowMin.toFixed(0)}-${yellowMax.toFixed(0)}\u00B0)`
                },
                {
                    min: redMin,
                    color: '#e74c3c',
                    label: `Danger (>${redMin.toFixed(0)}\u00B0)`
                }
            ],
            orient: 'vertical',
            left: 'left',
            top: 'center',
            textStyle: {
                color: '#fff',
                fontSize: 12
            }
        };
    } else {
        // Continuous Mode with thermal colormap
        visualMap = {
            type: 'continuous',
            min: 0,
            max: maxRoll,
            calculable: true,
            realtime: true,
            inRange: {
                color: [
                    '#0d47a1',  // Deep blue
                    '#1976d2',  // Blue
                    '#2196f3',  // Light blue
                    '#03a9f4',  // Sky blue
                    '#00bcd4',  // Cyan
                    '#00e676',  // Green-cyan
                    '#76ff03',  // Light green
                    '#ffeb3b',  // Yellow
                    '#ffc107',  // Amber
                    '#ff9800',  // Orange
                    '#ff5722',  // Deep orange
                    '#f44336',  // Red
                    '#d32f2f'   // Dark red
                ]
            },
            outOfRange: {
                color: '#b71c1c'  // Very dark red
            },
            orient: 'vertical',
            left: 'left',
            top: 'center',
            textStyle: {
                color: '#fff',
                fontSize: 12
            },
            formatter: function (value) {
                return value.toFixed(1) + '\u00B0';
            }
        };
    }

    // Calculate vessel position angle
    let vesselDisplayAngle;
    if (directionMode === 'northup') {
        vesselDisplayAngle = vesselHeading;
    } else {
        vesselDisplayAngle = 0;
    }

    // Chart options
    const option = {
        backgroundColor: '#1a1a1a',
        title: {
            text: 'Roll Angle (deg)',
            left: 'center',
            top: 20,
            textStyle: {
                color: '#fff',
                fontSize: 18,
                fontWeight: 'bold'
            }
        },
        tooltip: {
            trigger: 'item',
            formatter: function (params) {
                if (params.componentType === 'series') {
                    if (params.seriesName === 'Roll Response') {
                        const angle = params.value[0].toFixed(1);
                        const speed = params.value[1].toFixed(1);
                        const roll = params.value[2].toFixed(2);
                        const status = roll > maxRoll ? '\u26A0\uFE0F DANGER' :
                            roll > (maxRoll - 5) ? '\u26A0 CAUTION' : '\u2713 SAFE';
                        return `<b>Direction:</b> ${angle}\u00B0<br/>
                                <b>Speed:</b> ${speed} kn<br/>
                                <b>Roll:</b> ${roll}\u00B0<br/>
                                <b>Status:</b> ${status}`;
                    } else if (params.seriesName === 'Vessel Position') {
                        return `<b>Vessel Position</b><br/>
                                Heading: ${vesselHeading}\u00B0<br/>
                                Speed: ${vesselSpeed} kn`;
                    } else if (params.seriesName === 'Wave Direction') {
                        return `<b>Wave Direction</b><br/>
                                ${waveDirection.toFixed(0)}\u00B0`;
                    }
                }
                return '';
            }
        },
        visualMap: visualMap,
        polar: {
            center: ['50%', '55%'],
            radius: '65%'
        },
        angleAxis: {
            type: 'value',
            startAngle: startAngle,
            min: 0,
            max: 360,
            interval: 30,
            splitLine: {
                show: true,
                lineStyle: {
                    color: '#555',
                    width: 1,
                    type: 'solid'
                }
            },
            axisLabel: {
                formatter: '{value}\u00B0',
                color: '#fff',
                fontSize: 12
            },
            axisLine: {
                lineStyle: {
                    color: '#777',
                    width: 2
                }
            }
        },
        radiusAxis: {
            type: 'value',
            min: 0,
            max: Math.max(...radii) * 1.1,
            splitLine: {
                show: true,
                lineStyle: {
                    color: '#555',
                    width: 1,
                    type: 'solid'
                }
            },
            axisLabel: {
                formatter: '{value} kn',
                color: '#fff',
                fontSize: 11
            },
            axisLine: {
                lineStyle: {
                    color: '#777',
                    width: 2
                }
            }
        },
        series: [
            {
                name: 'Roll Response',
                type: 'scatter',
                coordinateSystem: 'polar',
                // KEY: Large symbols with high opacity create filled contour effect
                symbolSize: 25,  // Large enough to overlap and fill space
                data: heatmapData,
                itemStyle: {
                    opacity: 1,  // Full opacity for solid fill
                    borderWidth: 0  // No borders between points
                },
                large: true,
                largeThreshold: 2000,
                progressive: 1000,
                progressiveThreshold: 3000
            }
        ]
    };

    // Add vessel position marker
    if (vesselSpeed > 0) {
        option.series.push({
            name: 'Vessel Position',
            type: 'scatter',
            coordinateSystem: 'polar',
            symbolSize: 30,
            symbol: 'triangle',  // Triangle pointing up
            data: [[vesselDisplayAngle, vesselSpeed, 0]],
            itemStyle: {
                color: '#FFFFFF',
                borderColor: '#000000',
                borderWidth: 2,
                shadowBlur: 15,
                shadowColor: '#00FFFF'
            },
            label: {
                show: true,
                formatter: 'VESSEL',
                position: 'top',
                color: '#fff',
                fontSize: 10,
                fontWeight: 'bold',
                distance: 15,
                backgroundColor: 'rgba(0,0,0,0.7)',
                padding: [2, 5],
                borderRadius: 3
            },
            z: 100
        });
    }

    // Add wave direction indicator
    const maxDisplayRadius = Math.max(...radii) * 1.08;
    option.series.push({
        name: 'Wave Direction',
        type: 'scatter',
        coordinateSystem: 'polar',
        symbolSize: 25,
        symbol: 'arrow',  // Arrow symbol
        data: [[waveDirection, maxDisplayRadius, 0]],
        itemStyle: {
            color: '#FF1493',
            borderColor: '#FFFFFF',
            borderWidth: 2,
            shadowBlur: 10,
            shadowColor: '#FF1493'
        },
        label: {
            show: true,
            formatter: 'WAVE',
            position: 'top',
            color: '#fff',
            fontSize: 10,
            fontWeight: 'bold',
            distance: 15,
            backgroundColor: 'rgba(255,20,147,0.8)',
            padding: [2, 5],
            borderRadius: 3
        },
        z: 99
    });

    // Add compass labels
    const labelRadius = Math.max(...radii) * 1.18;
    if (directionMode === 'northup') {
        const compassLabels = [
            { angle: 0, label: 'N', color: '#FFD700' },
            { angle: 90, label: 'E', color: '#C0C0C0' },
            { angle: 180, label: 'S', color: '#C0C0C0' },
            { angle: 270, label: 'W', color: '#C0C0C0' }
        ];

        option.series.push({
            name: 'Compass',
            type: 'scatter',
            coordinateSystem: 'polar',
            symbolSize: 1,
            data: compassLabels.map(c => [c.angle, labelRadius, 0]),
            label: {
                show: true,
                formatter: function (params) {
                    return compassLabels[params.dataIndex].label;
                },
                color: function (params) {
                    return compassLabels[params.dataIndex].color;
                },
                fontSize: 18,
                fontWeight: 'bold',
                distance: 5
            },
            itemStyle: {
                opacity: 0
            },
            z: 1
        });
    } else {
        option.series.push({
            name: 'Compass',
            type: 'scatter',
            coordinateSystem: 'polar',
            symbolSize: 1,
            data: [[0, labelRadius, 0]],
            label: {
                show: true,
                formatter: 'BOW',
                color: '#FFD700',
                fontSize: 18,
                fontWeight: 'bold',
                distance: 5
            },
            itemStyle: {
                opacity: 0
            },
            z: 1
        });
    }

    // Set option and render
    chart.setOption(option, true);

    // Handle window resize
    const resizeHandler = function () {
        if (window.polarChart.instance) {
            window.polarChart.instance.resize();
        }
    };

    window.removeEventListener('resize', window.polarChart.resizeHandler);
    window.polarChart.resizeHandler = resizeHandler;
    window.addEventListener('resize', resizeHandler);
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
    if (window.polarChart.resizeHandler) {
        window.removeEventListener('resize', window.polarChart.resizeHandler);
        window.polarChart.resizeHandler = null;
    }
};