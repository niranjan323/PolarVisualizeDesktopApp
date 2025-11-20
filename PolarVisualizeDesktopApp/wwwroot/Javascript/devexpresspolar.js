window.devExpressPolarChart = window.devExpressPolarChart || {};

window.devExpressPolarChart.render = function (chartDataJson) {
    console.log('DevExpress Polar Chart - Custom Canvas Renderer');

    const canvas = document.getElementById('polarCanvas');
    if (!canvas) {
        console.error('Canvas not found');
        return;
    }

    const ctx = canvas.getContext('2d');
    const data = typeof chartDataJson === 'string' ? JSON.parse(chartDataJson) : chartDataJson;

    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;
    const maxRadius = Math.min(centerX, centerY) * 0.75;
    const minRadius = maxRadius * 0.1;

    // Clear canvas
    ctx.fillStyle = 'transparent';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Helper function
    function toRadians(deg) {
        return (deg - 90) * Math.PI / 180;
    }

    function polarToCartesian(angle, radius) {
        const rad = toRadians(angle);
        return {
            x: centerX + radius * Math.cos(rad),
            y: centerY + radius * Math.sin(rad)
        };
    }

    // Draw filled contours
    data.contours.forEach(contour => {
        if (contour.points.length < 3) return;

        ctx.beginPath();
        ctx.globalAlpha = contour.opacity;

        // Draw from center first
        ctx.moveTo(centerX, centerY);

        // Draw contour boundary
        contour.points.forEach((point, index) => {
            const radius = minRadius + (maxRadius - minRadius) * (point.speed / data.maxSpeed);
            const pos = polarToCartesian(point.angle, radius);

            if (index === 0) {
                ctx.lineTo(pos.x, pos.y);
            } else {
                ctx.lineTo(pos.x, pos.y);
            }
        });

        // Close path
        ctx.closePath();
        ctx.fillStyle = contour.color;
        ctx.fill();
    });

    ctx.globalAlpha = 1.0;

    // Draw grid lines
    ctx.strokeStyle = '#555';
    ctx.lineWidth = 1;

    // Radial circles (5, 10, 15, 20 kn)
    for (let i = 1; i <= 4; i++) {
        const ratio = i / 4;
        const r = minRadius + (maxRadius - minRadius) * ratio;

        ctx.beginPath();
        ctx.arc(centerX, centerY, r, 0, Math.PI * 2);
        ctx.stroke();

        // Speed labels
        const speed = i * 5;
        ctx.fillStyle = '#fff';
        ctx.font = '12px Arial';
        ctx.fillText(speed + ' kn', centerX + 5, centerY - r);
    }

    // Angular grid lines (every 30°)
    for (let angle = 0; angle < 360; angle += 30) {
        const pos = polarToCartesian(angle, maxRadius);

        ctx.beginPath();
        ctx.moveTo(centerX, centerY);
        ctx.lineTo(pos.x, pos.y);
        ctx.stroke();

        // Angle labels
        const labelPos = polarToCartesian(angle, maxRadius * 1.15);
        ctx.fillStyle = '#fff';
        ctx.font = '14px Arial';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(angle + '°', labelPos.x, labelPos.y);
    }

    // Draw compass labels
    if (data.directionMode === 'northup') {
        const compass = [
            { angle: 0, label: 'N', color: '#FFD700', size: '20px' },
            { angle: 90, label: 'E', color: '#C0C0C0', size: '18px' },
            { angle: 180, label: 'S', color: '#C0C0C0', size: '18px' },
            { angle: 270, label: 'W', color: '#C0C0C0', size: '18px' }
        ];

        compass.forEach(c => {
            const pos = polarToCartesian(c.angle, maxRadius * 1.25);
            ctx.fillStyle = c.color;
            ctx.font = `bold ${c.size} Arial`;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(c.label, pos.x, pos.y);
        });
    }

    // Draw vessel marker
    if (data.vesselSpeed > 0) {
        const vesselAngle = data.directionMode === 'northup' ? data.vesselHeading : 0;
        const vesselR = minRadius + (maxRadius - minRadius) * (data.vesselSpeed / data.maxSpeed);
        const vPos = polarToCartesian(vesselAngle, vesselR);

        ctx.save();
        ctx.translate(vPos.x, vPos.y);
        ctx.rotate(toRadians(vesselAngle) + Math.PI / 2);

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

        ctx.fillStyle = '#fff';
        ctx.font = 'bold 11px Arial';
        ctx.textAlign = 'center';
        ctx.fillText('VESSEL', vPos.x, vPos.y - 25);
    }

    // Draw wave marker
    const waveAngle = data.directionMode === 'northup'
        ? data.waveDirection
        : (data.waveDirection - data.vesselHeading);

    const waveR = maxRadius * 1.08;
    const wPos = polarToCartesian(waveAngle, waveR);

    ctx.save();
    ctx.translate(wPos.x, wPos.y);
    ctx.rotate(toRadians(waveAngle) + Math.PI / 2);

    ctx.beginPath();
    ctx.moveTo(0, -12);
    ctx.lineTo(-8, 8);
    ctx.lineTo(8, 8);
    ctx.closePath();

    ctx.fillStyle = '#4FC3F7';
    ctx.fill();
    ctx.strokeStyle = '#fff';
    ctx.lineWidth = 2;
    ctx.stroke();

    ctx.restore();

    ctx.fillStyle = '#fff';
    ctx.font = 'bold 11px Arial';
    ctx.textAlign = 'center';
    ctx.fillText('WAVE', wPos.x, wPos.y - 22);

    // Title
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 18px Arial';
    ctx.textAlign = 'center';
    ctx.fillText('Roll Angle (degrees)', centerX, 30);

    console.log('DevExpress polar chart rendered');
};

window.devExpressPolarChart.exportImage = function () {
    const canvas = document.getElementById('polarCanvas');
    if (!canvas) return;

    const link = document.createElement('a');
    link.download = 'polar-chart-devexpress.png';
    link.href = canvas.toDataURL('image/png');
    link.click();
};