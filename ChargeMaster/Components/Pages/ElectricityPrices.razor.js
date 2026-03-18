export function initializeChart(labels, data, currentTime) {
    console.log('initializeChart called with', labels.length, 'labels and', data.length, 'data points');

    // Ladda Chart.js från CDN om det inte redan är laddat
    if (typeof Chart === 'undefined') {
        console.log('Loading Chart.js from CDN...');
        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/chart.js';
        script.onload = () => {
            console.log('Chart.js loaded successfully');
            createChart(labels, data, currentTime);
        };
        script.onerror = () => {
            console.error('Failed to load Chart.js');
        };
        document.head.appendChild(script);
    } else {
        console.log('Chart.js already loaded');
        createChart(labels, data, currentTime);
    }
}

function createChart(labels, data, currentTime) {
    console.log('createChart called');
    const ctx = document.getElementById('priceChart');
    if (!ctx) {
        console.error('Canvas element with id "priceChart" not found');
        return;
    }

    console.log('Canvas found, creating chart...');

    // Förstör tidigare chart om den finns
    if (window.priceChartInstance) {
        console.log('Destroying previous chart instance');
        window.priceChartInstance.destroy();
    }

    // Beräkna min och max för Y-axel
    const min = Math.min(...data);
    const max = Math.max(...data);
    const padding = (max - min) * 0.1;

    console.log('Chart data range:', { min, max, padding });

    window.priceChartInstance = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Pris (SEK / kWh)',
                data: data,
                borderColor: '#0d6efd',
                backgroundColor: 'rgba(13, 110, 253, 0.1)',
                borderWidth: 2,
                fill: true,
                tension: 0.3,
                pointRadius: 4,
                pointBackgroundColor: '#0d6efd',
                pointBorderColor: '#fff',
                pointBorderWidth: 2,
                pointHoverRadius: 6
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: {
                    display: true,
                    position: 'top'
                },
                title: {
                    display: false
                },
                annotation: {
                    currentTime: currentTime,
                    currentTimeLabels: labels
                }
            },
            scales: {
                x: {
                    title: {
                        display: true,
                        text: 'Tid'
                    },
                    grid: {
                        drawBorder: true
                    }
                },
                y: {
                    title: {
                        display: true,
                        text: 'Pris (SEK / kWh)'
                    },
                    min: Math.max(0, min - padding),
                    max: max + padding,
                    grid: {
                        drawBorder: true
                    }
                }
            }
        },
        plugins: [{
            id: 'currentTimeIndicator',
            afterDraw(chart) {
                if (!currentTime) return;

                const ctx = chart.ctx;
                const xScale = chart.scales.x;
                const yScale = chart.scales.y;

                // Hitta index för aktuell tid
                const timeIndex = labels.findIndex(label => label === currentTime);
                if (timeIndex === -1) {
                    console.log('Current time not found in labels:', currentTime, 'available:', labels);
                    return;
                }

                console.log('Drawing current time indicator at index:', timeIndex);

                // Beräkna x-position för aktuell tid
                const xPos = xScale.getPixelForValue(timeIndex);

                // Rita lodrätt linje
                ctx.save();
                ctx.strokeStyle = '#0d6efd';
                ctx.lineWidth = 2;
                ctx.globalCompositeOperation = 'source-over';
                ctx.beginPath();
                ctx.moveTo(xPos, yScale.top);
                ctx.lineTo(xPos, yScale.bottom);
                ctx.stroke();
                ctx.restore();
            }
        }]
    });

    console.log('Chart created successfully');
}
