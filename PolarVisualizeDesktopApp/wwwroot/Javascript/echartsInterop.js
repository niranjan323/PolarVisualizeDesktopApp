window.polarChart = window.polarChart || {};

window.polarChart.renderChart = function (containerId, chartDataOrJson) {
    console.log('polarChart.renderChart called - Custom Canvas Renderer');

    const container = document.getElementById(containerId);
    if (!container) {
        console.error('Chart container not found:', containerId);
        return;
    }

    // Parse data
    let chartData = chartDataOrJson;
    if (typeof chartDataOrJson === 'string') {
        try {
            chartData = JSON.parse(chartDataOrJson);
        } catch (e) {
            console.error('Failed to parse chart data JSON:', e);
            return;
        }
    }

    const angles = chartData.angles || [];
    const radii = chartData.radii || [];
    const matrix = chartData.matrix || [];
    const maxRoll = chartData.maxRoll || 30;
    const mode = chartData.mode || 'continuous';
    const directionMode = chartData.directionMode || 'northup';
    const vesselHeading = chartData.vesselHeading || 0;
    const vesselSpeed = chartData.vesselSpeed || 0;
    const waveDirection = chartData.waveDirection || 0;

    console.log('Using custom canvas renderer with data:', {
        angleCount: angles.length,
        radiiCount: radii.length,
        mode: mode
    });

    // Clear container and create canvas
    container.innerHTML = '';

    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');

    // Set canvas size
    const size = Math.min(container.clientWidth, container.clientHeight);
    canvas.width = size;
    canvas.height = size;
    canvas.style.width = '100%';
    canvas.style.height = '100%';
    container.appendChild(canvas);

    // Store for cleanup
    window.polarChart.canvas = canvas;
    window.polarChart.container = container;

    // Chart parameters
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;
    const maxRadius = Math.min(centerX, centerY) * 0.75;
    const minRadius = maxRadius * 0.1;

    // Helper function: angle to radians
    function toRadians(deg) {
        return (deg - 90) * Math.PI / 180; // -90 to start from top
    }

    // Helper function: get color for roll value
    function getColor(rollValue) {
        if (mode === 'trafficlight') {
            if (rollValue <= maxRoll - 5) return '#2ecc71'; // Green
            if (rollValue <= maxRoll) return '#f39c12'; // Orange
            return '#e74c3c'; // Red
        } else {
            // Continuous gradient
            const ratio = Math.min(rollValue / maxRoll, 1);

            if (ratio < 0.2) return `rgb(${Math.floor(13 + ratio * 5 * 150)}, ${Math.floor(71 + ratio * 5 * 100)}, ${Math.floor(161)})`;
            if (ratio < 0.4) return `rgb(${Math.floor(25 + (ratio - 0.2) * 5 * 100)}, ${Math.floor(118 + (ratio - 0.2) * 5 * 50)}, ${Math.floor(210 - (ratio - 0.2) * 5 * 50)})`;
            if (ratio < 0.6) return `rgb(${Math.floor(0 + (ratio - 0.4) * 5 * 150)}, ${Math.floor(188 - (ratio - 0.4) * 5 * 50)}, ${Math.floor(212 - (ratio - 0.4) * 5 * 100)})`;
            if (ratio < 0.8) return `rgb(${Math.floor(118 + (ratio - 0.6) * 5 * 137)}, ${Math.floor(255 - (ratio - 0.6) * 5 * 100)}, ${Math.floor(3 + (ratio - 0.6) * 5 * 100)})`;
            return `rgb(${Math.floor(255)}, ${Math.floor(152 - (ratio - 0.8) * 5 * 100)}, ${Math.floor(0)})`;
        }
    }

    // Interpolation function
    function interpolateValue(angle, radius) {
        // Find nearest angles
        let a1 = 0, a2 = 0;
        let minDiff = 360;

        for (let i = 0; i < angles.length; i++) {
            let diff = Math.abs(angles[i] - angle);
            if (diff > 180) diff = 360 - diff;
            if (diff < minDiff) {
                minDiff = diff;
                a1 = i;
            }
        }

        // Find second nearest
        minDiff = 360;
        for (let i = 0; i < angles.length; i++) {
            if (i === a1) continue;
            let diff = Math.abs(angles[i] - angle);
            if (diff > 180) diff = 360 - diff;
            if (diff < minDiff) {
                minDiff = diff;
                a2 = i;
            }
        }

        // Find nearest radii
        let r1 = 0, r2 = radii.length - 1;
        for (let i = 0; i < radii.length - 1; i++) {
            if (radius >= radii[i] && radius <= radii[i + 1]) {
                r1 = i;
                r2 = i + 1;
                break;
            }
        }

        // Bilinear interpolation
        const rFactor = radii[r2] > radii[r1] ? (radius - radii[r1]) / (radii[r2] - radii[r1]) : 0;
        const v1 = (matrix[r1][a1] || 0) * (1 - rFactor) + (matrix[r2][a1] || 0) * rFactor;
        const v2 = (matrix[r1][a2] || 0) * (1 - rFactor) + (matrix[r2][a2] || 0) * rFactor;

        return (v1 + v2) / 2;
    }

    // Clear canvas
    ctx.fillStyle = '#1a1a1a';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Draw filled contours using wedges
    const numAngularSegments = 360; // 1 degree resolution
    const numRadialSegments = 50; // Radial segments

    const maxSpeed = Math.max(...radii);

    for (let r = 0; r < numRadialSegments; r++) {
        const innerRatio = r / numRadialSegments;
        const outerRatio = (r + 1) / numRadialSegments;

        const innerRadius = minRadius + (maxRadius - minRadius) * innerRatio;
        const outerRadius = minRadius + (maxRadius - minRadius) * outerRatio;

        const innerSpeed = maxSpeed * innerRatio;
        const outerSpeed = maxSpeed * outerRatio;

        for (let a = 0; a < numAngularSegments; a++) {
            const angle1 = (a / numAngularSegments) * 360;
            const angle2 = ((a + 1) / numAngularSegments) * 360;

            // Get roll value for this segment
            const midAngle = (angle1 + angle2) / 2;
            const midRadius = (innerSpeed + outerSpeed) / 2;
            const rollValue = interpolateValue(midAngle, midRadius);

            // Get color
            const color = getColor(rollValue);

            // Draw wedge segment
            ctx.beginPath();
            ctx.moveTo(centerX, centerY);

            const rad1 = toRadians(angle1);
            const rad2 = toRadians(angle2);

            // Draw filled wedge
            ctx.arc(centerX, centerY, innerRadius, rad1, rad2, false);
            ctx.lineTo(
                centerX + outerRadius * Math.cos(rad2),
                centerY + outerRadius * Math.sin(rad2)
            );
            ctx.arc(centerX, centerY, outerRadius, rad2, rad1, true);
            ctx.closePath();

            ctx.fillStyle = color;
            ctx.fill();
        }
    }

    // Draw grid lines
    ctx.strokeStyle = '#555';
    ctx.lineWidth = 1;

    // Radial grid lines
    for (let i = 1; i <= 4; i++) {
        const r = minRadius + (maxRadius - minRadius) * (i / 4);
        ctx.beginPath();
        ctx.arc(centerX, centerY, r, 0, Math.PI * 2);
        ctx.stroke();

        const speed = i * 5;
        ctx.fillStyle = '#fff';
        ctx.font = '12px Arial';
        ctx.fillText(speed + ' kn', centerX + 5, centerY - r);
    }

    // Angular grid lines
    for (let angle = 0; angle < 360; angle += 30) {
        const rad = toRadians(angle);
        ctx.beginPath();
        ctx.moveTo(centerX, centerY);
        ctx.lineTo(
            centerX + maxRadius * Math.cos(rad),
            centerY + maxRadius * Math.sin(rad)
        );
        ctx.stroke();

        // Angle labels
        const labelRadius = maxRadius * 1.15;
        const x = centerX + labelRadius * Math.cos(rad);
        const y = centerY + labelRadius * Math.sin(rad);

        ctx.fillStyle = '#fff';
        ctx.font = '14px Arial';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(angle + '\u00B0', x, y);
    }

    // Draw compass labels
    if (directionMode === 'northup') {
        const compassRadius = maxRadius * 1.25;
        const compass = [
            { angle: 0, label: 'N', color: '#FFD700' },
            { angle: 90, label: 'E', color: '#C0C0C0' },
            { angle: 180, label: 'S', color: '#C0C0C0' },
            { angle: 270, label: 'W', color: '#C0C0C0' }
        ];

        compass.forEach(c => {
            const rad = toRadians(c.angle);
            const x = centerX + compassRadius * Math.cos(rad);
            const y = centerY + compassRadius * Math.sin(rad);

            ctx.fillStyle = c.color;
            ctx.font = 'bold 20px Arial';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(c.label, x, y);
        });
    }

    // Draw vessel position
    if (vesselSpeed > 0) {
        const vesselAngle = directionMode === 'northup' ? vesselHeading : 0;
        const vesselRad = toRadians(vesselAngle);
        const vesselR = minRadius + (maxRadius - minRadius) * (vesselSpeed / maxSpeed);

        const vx = centerX + vesselR * Math.cos(vesselRad);
        const vy = centerY + vesselR * Math.sin(vesselRad);

        // Draw triangle
        ctx.save();
        ctx.translate(vx, vy);
        ctx.rotate(vesselRad + Math.PI / 2);

        ctx.beginPath();
        ctx.moveTo(0, -15);
        ctx.lineTo(-10, 10);
        ctx.lineTo(10, 10);
        ctx.closePath();

        ctx.fillStyle = '#FFFFFF';
        ctx.fill();
        ctx.strokeStyle = '#000';
        ctx.lineWidth = 2;
        ctx.stroke();

        ctx.restore();

        // Label
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 11px Arial';
        ctx.textAlign = 'center';
        ctx.fillText('VESSEL', vx, vy - 25);
    }

    // Draw wave direction
    const waveRad = toRadians(waveDirection);
    const waveR = maxRadius * 1.08;
    const wx = centerX + waveR * Math.cos(waveRad);
    const wy = centerY + waveR * Math.sin(waveRad);

    ctx.save();
    ctx.translate(wx, wy);
    ctx.rotate(waveRad + Math.PI / 2);

    ctx.beginPath();
    ctx.moveTo(0, -12);
    ctx.lineTo(-8, 8);
    ctx.lineTo(8, 8);
    ctx.closePath();

    ctx.fillStyle = '#FF1493';
    ctx.fill();
    ctx.strokeStyle = '#fff';
    ctx.lineWidth = 2;
    ctx.stroke();

    ctx.restore();

    ctx.fillStyle = '#fff';
    ctx.font = 'bold 11px Arial';
    ctx.textAlign = 'center';
    ctx.fillText('WAVE', wx, wy - 22);

    // Draw legend
    const legendX = 30;
    const legendY = canvas.height / 2 - 60;
    const legendWidth = 40;
    const legendHeight = 120;

    if (mode === 'trafficlight') {
        // Traffic light legend
        const boxes = [
            { color: '#2ecc71', label: `Safe (0-${(maxRoll - 5).toFixed(0)}\u00B0)` },
            { color: '#f39c12', label: `Caution (${(maxRoll - 5).toFixed(0)}-${maxRoll.toFixed(0)}\u00B0)` },
            { color: '#e74c3c', label: `Danger (>${maxRoll.toFixed(0)}\u00B0)` }
        ];

        boxes.forEach((box, i) => {
            const y = legendY + i * 40;
            ctx.fillStyle = box.color;
            ctx.fillRect(legendX, y, 20, 20);

            ctx.fillStyle = '#fff';
            ctx.font = '11px Arial';
            ctx.textAlign = 'left';
            ctx.fillText(box.label, legendX + 25, y + 14);
        });
    } else {
        // Continuous legend
        const gradient = ctx.createLinearGradient(legendX, legendY, legendX, legendY + legendHeight);
        gradient.addColorStop(0, '#d32f2f');
        gradient.addColorStop(0.25, '#ff9800');
        gradient.addColorStop(0.5, '#ffeb3b');
        gradient.addColorStop(0.75, '#00bcd4');
        gradient.addColorStop(1, '#0d47a1');

        ctx.fillStyle = gradient;
        ctx.fillRect(legendX, legendY, 20, legendHeight);

        ctx.strokeStyle = '#fff';
        ctx.strokeRect(legendX, legendY, 20, legendHeight);

        ctx.fillStyle = '#fff';
        ctx.font = '11px Arial';
        ctx.textAlign = 'left';
        ctx.fillText(maxRoll.toFixed(0) + '\u00B0', legendX + 25, legendY + 10);
        ctx.fillText('0\u00B0', legendX + 25, legendY + legendHeight - 5);
    }

    // Title
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 18px Arial';
    ctx.textAlign = 'center';
    ctx.fillText('Roll Angle (deg)', centerX, 30);

    console.log('Custom canvas rendering complete');
};

window.polarChart.exportImage = function () {
    if (window.polarChart.canvas) {
        return window.polarChart.canvas.toDataURL('image/png');
    }
    return null;
};

window.polarChart.dispose = function () {
    if (window.polarChart.container) {
        window.polarChart.container.innerHTML = '';
    }
    window.polarChart.canvas = null;
    window.polarChart.container = null;
};