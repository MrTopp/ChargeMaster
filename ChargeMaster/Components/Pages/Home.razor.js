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
    const ctx = document.getElementById('priceChartHome');
    if (!ctx) {
        console.error('Canvas element with id "priceChartHome" not found');
        return;
    }

    console.log('Canvas found, creating chart...');

    // Förstör tidigare chart om den finns
    if (window.priceChartHomeInstance) {
        console.log('Destroying previous chart instance');
        window.priceChartHomeInstance.destroy();
    }

    // Beräkna min och max för Y-axel
    const min = Math.min(...data);
    const max = Math.max(...data);
    const padding = (max - min) * 0.1;

    console.log('Chart data range:', { min, max, padding });

    window.priceChartHomeInstance = new Chart(ctx, {
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

                // Konvertera aktuell tid till minuter från midnatt
                const [hours, minutes] = currentTime.split(':').map(Number);
                const currentTimeMinutes = hours * 60 + minutes;

                // Konvertera alla etiketter till minuter från midnatt
                const labelMinutes = labels.map(label => {
                    const [h, m] = label.split(':').map(Number);
                    return h * 60 + m;
                });

                // Hitta positionen för aktuell tid inom X-axelns skala
                const firstTimeMinutes = labelMinutes[0];
                const lastTimeMinutes = labelMinutes[labelMinutes.length - 1];

                // Om aktuell tid är före första eller efter sista etikett, rita inte linjen
                if (currentTimeMinutes < firstTimeMinutes || currentTimeMinutes > lastTimeMinutes) {
                    console.log('Current time', currentTime, 'is outside the chart range');
                    return;
                }

                // Beräkna normaliseringsvärdena för interpolation
                const relativePosition = (currentTimeMinutes - firstTimeMinutes) / (lastTimeMinutes - firstTimeMinutes);

                // Få pixel-positionen mellan första och sista label
                const firstXPos = xScale.getPixelForValue(0);
                const lastXPos = xScale.getPixelForValue(labelMinutes.length - 1);
                const xPos = firstXPos + (lastXPos - firstXPos) * relativePosition;

                console.log('Drawing current time indicator at position:', {
                    currentTime,
                    currentTimeMinutes,
                    firstTimeMinutes,
                    lastTimeMinutes,
                    relativePosition,
                    xPos
                });

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
