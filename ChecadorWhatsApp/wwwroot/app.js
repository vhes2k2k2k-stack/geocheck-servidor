// GeoCheck Móvil - Frontend Logic
let map = null;
let mapMarkers = [];
let mapCircles = [];
let empleadosCache = [];
let sucursalesCache = [];
let currentReportSubtab = 'resumen';
let previewGpsCoords = { lat: 19.429031, lon: -99.162458 };

document.addEventListener('DOMContentLoaded', () => {
    initNavigation();
    initMap();
    loadDashboard();
    loadEmpleados();
    loadSucursales();
    loadConfiguracion();
    setupReportEvents();
    setupPreviewEvents();

    // Consultar IP real de red del servidor
    fetch('/api/servidor/info')
        .then(r => r.json())
        .then(data => {
            const txtIp = document.getElementById('txtSugerenciaIp');
            if (txtIp) txtIp.textContent = data.urlSugerida;
            const cfgServidor = document.getElementById('cfgServidorUrl');
            if (cfgServidor && (!cfgServidor.value || cfgServidor.value.includes('localhost'))) {
                cfgServidor.value = data.urlSugerida;
            }
        })
        .catch(() => {
            const txtIp = document.getElementById('txtSugerenciaIp');
            if (txtIp) txtIp.textContent = 'http://192.168.1.14:5097';
        });

    // Auto-refresh Dashboard cada 12 segundos
    setInterval(() => {
        const activeTab = document.querySelector('.nav-tab.active')?.dataset.tab;
        if (activeTab === 'dashboard') {
            loadDashboard(false);
        }
    }, 12000);
});

// ==========================================
// 1. NAVIGATION TABS
// ==========================================
function initNavigation() {
    const tabs = document.querySelectorAll('.nav-tab');
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            tabs.forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));

            tab.classList.add('active');
            const targetId = tab.dataset.tab;
            const targetPane = document.getElementById(targetId);
            if (targetPane) targetPane.classList.add('active');

            if (targetId === 'dashboard') {
                setTimeout(() => {
                    if (map) map.invalidateSize();
                }, 200);
                loadDashboard();
            } else if (targetId === 'reportes') {
                generarReportes();
            } else if (targetId === 'empleados') {
                renderEmpleadosTable();
            } else if (targetId === 'sucursales') {
                renderSucursalesTable();
            }
        });
    });
}

// ==========================================
// 2. LEAFLET INTERACTIVE MAP
// ==========================================
function initMap() {
    const mapEl = document.getElementById('mapContainer');
    if (!mapEl) return;

    map = L.map('mapContainer').setView([19.429031, -99.162458], 12);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);
}

function updateMap(sucursales, checadas) {
    if (!map) return;

    mapMarkers.forEach(m => map.removeLayer(m));
    mapCircles.forEach(c => map.removeLayer(c));
    mapMarkers = [];
    mapCircles = [];

    const bounds = L.latLngBounds();

    // 1. Dibujar círculos de geocercas
    sucursales.forEach(suc => {
        if (!suc.activa) return;
        const circle = L.circle([suc.latitud, suc.longitud], {
            color: suc.colorHex || '#2563EB',
            fillColor: suc.colorHex || '#2563EB',
            fillOpacity: 0.15,
            radius: suc.radioMetros
        }).addTo(map);

        circle.bindPopup(`
            <div style="font-size:13px; line-height:1.4;">
                <strong style="color:#047857; font-size:14px;">🏢 ${suc.nombre}</strong><br/>
                <span style="color:#666;">${suc.direccion}</span><br/>
                <strong>Radio geocerca:</strong> ${suc.radioMetros} metros<br/>
                <strong>Coordenadas:</strong> ${suc.latitud.toFixed(5)}, ${suc.longitud.toFixed(5)}
            </div>
        `);

        mapCircles.push(circle);
        bounds.extend([suc.latitud, suc.longitud]);
    });

    // 2. Dibujar pines de las checadas
    checadas.forEach(c => {
        if (!c.latitud || !c.longitud) return;

        let iconColor = '#10B981';
        let iconHtml = '🟢';
        if (c.estatus === 1) {
            iconColor = '#F59E0B';
            iconHtml = '🟡';
        } else if (c.estatus === 2 || !c.enGeocerca) {
            iconColor = '#EF4444';
            iconHtml = '🔴';
        }

        const customIcon = L.divIcon({
            className: 'custom-pin',
            html: `<div style="background:${iconColor}; width:30px; height:30px; border-radius:50%; display:flex; align-items:center; justify-content:center; color:white; font-size:14px; box-shadow:0 3px 8px rgba(0,0,0,0.3); border:2px solid white;">${iconHtml}</div>`,
            iconSize: [30, 30],
            iconAnchor: [15, 15]
        });

        const marker = L.marker([c.latitud, c.longitud], { icon: customIcon }).addTo(map);
        const fechaStr = new Date(c.fechaHora).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const distStr = c.distanciaMetros ? (c.distanciaMetros < 1000 ? `${Math.round(c.distanciaMetros)} m` : `${(c.distanciaMetros / 1000).toFixed(2)} km`) : '0 m';

        marker.bindPopup(`
            <div style="font-size:13px; line-height:1.45; min-width:200px;">
                <div style="font-weight:700; font-size:14px; color:#1e293b;">${c.empleadoNombre}</div>
                <div style="color:#64748b; font-size:12px; margin-bottom:6px;">${c.departamento}</div>
                <hr style="margin:4px 0; border:none; border-top:1px solid #e2e8f0;"/>
                <strong>Tipo:</strong> ${c.tipoTexto} ${c.tipo === 0 ? '🟢' : (c.tipo === 3 ? '🔴' : '🍔')}<br/>
                <strong>Hora:</strong> ${fechaStr}<br/>
                <strong>Estatus:</strong> <span style="font-weight:600; color:${iconColor};">${c.estatusTexto}</span><br/>
                <strong>Origen:</strong> ${c.origen}<br/>
                <strong>Sede de Trabajo Asignada:</strong> ${c.sucursalAsignadaNombre || c.sucursalCercanaNombre || 'N/A'}<br/>
                <strong>Distancia a sede:</strong> ${distStr} (${c.enGeocerca ? '🟢 En zona' : '🔴 Fuera de zona'})<br/>
                ${c.enlaceGoogleMaps ? `<div style="margin-top:8px;"><a href="${c.enlaceGoogleMaps}" target="_blank" style="background:#047857; color:white; padding:4px 10px; border-radius:4px; text-decoration:none; font-size:12px; font-weight:600; display:inline-block;">🗺️ Ver en Google Maps</a></div>` : ''}
            </div>
        `);

        mapMarkers.push(marker);
        bounds.extend([c.latitud, c.longitud]);
    });

    if (bounds.isValid()) {
        map.fitBounds(bounds, { padding: [40, 40] });
    }
}

// ==========================================
// 3. DASHBOARD DATA
// ==========================================
async function loadDashboard(updateMapView = true) {
    try {
        const [statsRes, sucursalesRes] = await Promise.all([
            fetch('/api/dashboard/stats'),
            fetch('/api/sucursales')
        ]);

        const stats = await statsRes.json();
        const sucursales = await sucursalesRes.json();
        sucursalesCache = sucursales;

        document.getElementById('statTotalEmpleados').textContent = stats.totalEmpleadosActivos;
        document.getElementById('statChecadasHoy').textContent = stats.totalChecadasHoy;
        document.getElementById('statATiempo').textContent = stats.entradasATiempoHoy;
        document.getElementById('statRetardos').textContent = stats.retardosHoy;
        document.getElementById('statFueraZona').textContent = stats.fueraDeZonaHoy;

        renderLiveFeed(stats.ultimasChecadas);

        if (updateMapView) {
            updateMap(sucursales, stats.ultimasChecadas);
        }
    } catch (err) {
        console.error('Error al cargar dashboard:', err);
    }
}

function renderLiveFeed(checadas) {
    const feedContainer = document.getElementById('liveFeedList');
    if (!feedContainer) return;

    if (!checadas || checadas.length === 0) {
        feedContainer.innerHTML = '<div style="text-align:center; padding:2rem; color:#94a3b8;">No hay registros el día de hoy.</div>';
        return;
    }

    feedContainer.innerHTML = checadas.map(c => {
        const initials = c.empleadoNombre.split(' ').map(n => n[0]).slice(0, 2).join('');
        const timeStr = new Date(c.fechaHora).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

        let badgeClass = 'badge-success';
        if (c.estatus === 1) badgeClass = 'badge-warning';
        if (c.estatus === 2 || !c.enGeocerca) badgeClass = 'badge-danger';

        const distStr = c.distanciaMetros ? (c.distanciaMetros < 1000 ? `${Math.round(c.distanciaMetros)}m` : `${(c.distanciaMetros / 1000).toFixed(1)}km`) : '';

        const avatarHtml = c.fotoSelfieUrl
            ? `<div class="feed-avatar" style="overflow:hidden; cursor:pointer; padding:0; border:2px solid #10b981;" onclick="event.stopPropagation(); verSelfieGrande('${c.fotoSelfieUrl}', '${c.empleadoNombre.replace(/'/g, "\\'")}', '${timeStr}', '${c.departamento || ''}', '${distStr}')" title="Ver selfie tomada en Entrada"><img src="${c.fotoSelfieUrl}" style="width:100%; height:100%; object-fit:cover;" /></div>`
            : `<div class="feed-avatar">${initials}</div>`;

        return `
            <div class="feed-item" onclick="zoomToChecada(${c.latitud}, ${c.longitud})">
                ${avatarHtml}
                <div class="feed-body">
                    <div class="feed-title">
                        <span class="feed-name">${c.empleadoNombre}</span>
                        <div>
                            <span class="feed-time" style="margin-right:6px;">${timeStr}</span>
                            <button class="btn btn-sm" onclick="event.stopPropagation(); borrarChecada('${c.id}')" style="background:#fee2e2; color:#dc2626; border:1px solid #fca5a5; padding:1px 5px; border-radius:4px; font-size:10px; cursor:pointer;" title="Eliminar este registro">🗑️</button>
                        </div>
                    </div>
                    <div class="feed-desc">
                        <strong>${c.tipoTexto}</strong>
                        <span class="badge ${badgeClass}">${c.estatusTexto}</span>
                        ${c.fotoSelfieUrl ? `<span class="badge badge-success" style="font-size:10px; cursor:pointer;" onclick="event.stopPropagation(); verSelfieGrande('${c.fotoSelfieUrl}', '${c.empleadoNombre.replace(/'/g, "\\'")}', '${timeStr}', '${c.departamento || ''}', '${distStr}')">📸 Con Selfie</span>` : ''}
                        <span class="badge badge-secondary" style="font-size:10px;">${c.origen}</span>
                        ${(c.sucursalAsignadaNombre || c.sucursalCercanaNombre) ? `<span style="color:#64748b;">• Sede: ${c.sucursalAsignadaNombre || c.sucursalCercanaNombre} (${distStr})</span>` : ''}
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

function zoomToChecada(lat, lon) {
    if (map && lat && lon) {
        map.setView([lat, lon], 16, { animate: true });
        const marker = mapMarkers.find(m => {
            const p = m.getLatLng();
            return Math.abs(p.lat - lat) < 0.0001 && Math.abs(p.lng - lon) < 0.0001;
        });
        if (marker) marker.openPopup();
    }
}

// ==========================================
// 4. VISTA PREVIA Y SIMULADOR DE APP MÓVIL
// ==========================================
function setupPreviewEvents() {
    const sel = document.getElementById('previewEmpSelect');
    if (sel) {
        sel.addEventListener('change', () => {
            actualizarEstadoPreview(sel.value);
            sincronizarGpsPreviewConEmpleado(sel.value);
        });
    }
}

function sincronizarGpsPreviewConEmpleado(empId) {
    const emp = empleadosCache.find(e => e.id === empId);
    const modo = document.getElementById('previewModoGps')?.value || 'en_sede';

    if (modo === 'gps_real') {
        actualizarGpsPreview();
        return;
    }

    if (emp && emp.sucursalId) {
        const suc = sucursalesCache.find(s => s.id === emp.sucursalId);
        if (suc && suc.latitud && suc.longitud) {
            if (modo === 'fuera_sede') {
                previewGpsCoords = { lat: suc.latitud + 0.15, lon: suc.longitud + 0.15 };
                document.getElementById('previewCoordsGps').textContent = `📍 Lat: ${previewGpsCoords.lat.toFixed(5)}, Lon: ${previewGpsCoords.lon.toFixed(5)}`;
                document.getElementById('previewGeocercaGps').innerHTML = '<span style="color:#dc2626;">🔴 Fuera de rango (a ~16km)</span>';
            } else {
                previewGpsCoords = { lat: suc.latitud, lon: suc.longitud };
                document.getElementById('previewCoordsGps').textContent = `📍 Lat: ${suc.latitud.toFixed(5)}, Lon: ${suc.longitud.toFixed(5)}`;
                document.getElementById('previewGeocercaGps').innerHTML = '<span style="color:#059669;">🟢 En punto de trabajo asignado (0m)</span>';
            }
            return;
        }
    }

    // Fallback general
    if (modo === 'fuera_sede') {
        previewGpsCoords = { lat: 19.55000, lon: -99.25000 };
        document.getElementById('previewCoordsGps').textContent = '📍 Lat: 19.55000, Lon: -99.25000';
        document.getElementById('previewGeocercaGps').innerHTML = '<span style="color:#dc2626;">🔴 Fuera de rango</span>';
    } else {
        previewGpsCoords = { lat: 19.42903, lon: -99.16246 };
        document.getElementById('previewCoordsGps').textContent = '📍 Lat: 19.42903, Lon: -99.16246';
        document.getElementById('previewGeocercaGps').innerHTML = '<span style="color:#059669;">🟢 En zona permitida</span>';
    }
}

function cambiarModoGpsPreview() {
    const sel = document.getElementById('previewEmpSelect');
    sincronizarGpsPreviewConEmpleado(sel ? sel.value : null);
}

function actualizarGpsPreview() {
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            pos => {
                previewGpsCoords = { lat: pos.coords.latitude, lon: pos.coords.longitude };
                document.getElementById('previewCoordsGps').textContent = `📍 Lat: ${pos.coords.latitude.toFixed(5)}, Lon: ${pos.coords.longitude.toFixed(5)}`;
                document.getElementById('previewGeocercaGps').innerHTML = '<span style="color:#059669;">🟢 GPS Real Sincronizado</span>';
            },
            err => {
                document.getElementById('previewCoordsGps').textContent = '📍 Lat: 19.42903, Lon: -99.16246';
                document.getElementById('previewGeocercaGps').innerHTML = '<span style="color:#64748b;">📍 GPS por Defecto</span>';
            }
        );
    }
}

async function checarDesdePreview(tipo) {
    const sel = document.getElementById('previewEmpSelect');
    const empId = sel ? sel.value : (empleadosCache[0]?.id || 'EMP-001');
    const emp = empleadosCache.find(e => e.id === empId);

    const nip = prompt(`🔐 Ingresa el NIP asignado para ${emp?.nombre || 'el colaborador'}:`, "1234");
    if (!nip) {
        alert('Marcaje cancelado: Se requiere ingresar el NIP asignado.');
        return;
    }

    let fotoBase64 = null;
    if (tipo === 'Entrada') {
        fotoBase64 = await capturarSelfieWeb(emp?.nombre || 'Colaborador');
    }

    try {
        const res = await fetch('/api/movil/checar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                empleadoId: empId,
                pin: nip.trim(),
                tipo: tipo,
                latitud: previewGpsCoords.lat,
                longitud: previewGpsCoords.lon,
                precisionMetros: 10,
                dispositivo: 'App Móvil (Web Preview)',
                fotoBase64: fotoBase64
            })
        });

        const data = await res.json();
        if (data.exito) {
            let msg = `✅ ${data.mensaje}\n\nEstatus: ${data.estatusTexto}`;
            if (data.fotoSelfieUrl) {
                msg += '\n📸 ¡Selfie capturada y guardada en el sistema para verificación facial en localhost!';
            }
            alert(msg);
            loadDashboard(true);
            actualizarEstadoPreview(empId);
        } else {
            alert(`❌ ${data.mensaje}`);
        }
    } catch (e) {
        alert('Error al comunicar con el servidor.');
    }
}

async function actualizarEstadoPreview(empId) {
    if (!empId) return;
    try {
        const res = await fetch(`/api/movil/estado/${empId}`);
        if (res.ok) {
            const data = await res.json();
            const t = document.getElementById('previewStatusTitle');
            const d = document.getElementById('previewStatusDetails');
            if (t) t.textContent = `Estado: ${data.estadoJornada}`;
            if (d) {
                d.innerHTML = `
                    🟢 Entrada: ${data.horaEntrada || '--'}<br>
                    🍔 Comida: ${data.horaSalidaComer || '--'}<br>
                    🔙 Regreso: ${data.horaRegresoComer || '--'}<br>
                    🔴 Salida: ${data.horaSalida || '--'}<br>
                    ⏱️ Horas trabajadas: ${data.tiempoTrabajado || '--'}
                `;
            }
        }
    } catch (e) {}
}

// ==========================================
// 5. REPORTES & INFORMES
// ==========================================
function setupReportEvents() {
    document.querySelectorAll('.report-subtab').forEach(tab => {
        tab.addEventListener('click', () => {
            document.querySelectorAll('.report-subtab').forEach(t => t.classList.remove('active'));
            tab.classList.add('active');
            currentReportSubtab = tab.dataset.subtab;
            generarReportes();
        });
    });

    document.getElementById('btnFiltrarReporte')?.addEventListener('click', generarReportes);
    document.getElementById('btnExportarCsv')?.addEventListener('click', exportarCsv);
    document.getElementById('btnImprimirReporte')?.addEventListener('click', () => window.print());
}

async function generarReportes() {
    const fechaInicio = document.getElementById('filtroFechaInicio')?.value;
    const fechaFin = document.getElementById('filtroFechaFin')?.value;
    const empleadoId = document.getElementById('filtroEmpleado')?.value;
    const depto = document.getElementById('filtroDepto')?.value;

    const params = new URLSearchParams();
    if (fechaInicio) params.append('fechaInicio', fechaInicio);
    if (fechaFin) params.append('fechaFin', fechaFin);
    if (empleadoId) params.append('empleadoId', empleadoId);
    if (depto) params.append('departamento', depto);

    const container = document.getElementById('reportTableContainer');
    container.innerHTML = '<div style="text-align:center; padding:2rem; color:#64748b;">Cargando informe...</div>';

    try {
        if (currentReportSubtab === 'resumen') {
            const res = await fetch(`/api/reportes/resumen?${params.toString()}`);
            const data = await res.json();
            renderReporteResumen(data, container);
        } else if (currentReportSubtab === 'bitacora') {
            const res = await fetch(`/api/checadas?${params.toString()}`);
            const data = await res.json();
            renderReporteBitacora(data, container);
        } else if (currentReportSubtab === 'incidencias') {
            const res = await fetch(`/api/checadas?${params.toString()}`);
            const data = await res.json();
            const incidencias = data.filter(c => c.estatus === 1 || c.estatus === 2 || !c.enGeocerca);
            renderReporteIncidencias(incidencias, container);
        }
    } catch (err) {
        container.innerHTML = '<div style="color:#ef4444; padding:1rem;">Error al generar el informe.</div>';
    }
}

function renderReporteResumen(resumenes, container) {
    if (!resumenes || resumenes.length === 0) {
        container.innerHTML = '<div style="text-align:center; padding:2rem; color:#94a3b8;">No se encontraron registros en el periodo seleccionado.</div>';
        return;
    }

    container.innerHTML = `
        <table class="data-table">
            <thead>
                <tr>
                    <th>Empleado</th>
                    <th>Departamento</th>
                    <th>Fecha</th>
                    <th>Entrada</th>
                    <th>Comida</th>
                    <th>Salida</th>
                    <th>Horas Totales</th>
                    <th>Horas Efectivas</th>
                    <th>Cumplimiento GPS</th>
                    <th>Estado Jornada</th>
                </tr>
            </thead>
            <tbody>
                ${resumenes.map(r => {
                    const fStr = new Date(r.fecha).toLocaleDateString([], { day: '2-digit', month: '2-digit', year: 'numeric' });
                    const inStr = r.horaEntrada ? new Date(r.horaEntrada).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '--';
                    const outStr = r.horaSalida ? new Date(r.horaSalida).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '--';
                    
                    let comidaStr = '--';
                    if (r.horaSalidaComer && r.horaRegresoComer) {
                        comidaStr = `${r.minutosComida} min`;
                    } else if (r.horaSalidaComer) {
                        comidaStr = 'En curso';
                    }

                    const inBadge = r.estatusEntrada === 1 
                        ? `<span class="badge badge-warning">Retardo (${r.minutosRetardo}m)</span>` 
                        : (r.horaEntrada ? `<span class="badge badge-success">A tiempo</span>` : '');

                    const gpsBadge = r.cumplioGeocerca 
                        ? `<span class="badge badge-success">🟢 En Zona</span>` 
                        : `<span class="badge badge-danger">🔴 Fuera de Zona</span>`;

                    return `
                        <tr>
                            <td><strong>${r.empleadoNombre}</strong></td>
                            <td>${r.departamento}</td>
                            <td>${fStr}</td>
                            <td>${inStr} ${inBadge}</td>
                            <td>${comidaStr}</td>
                            <td>${outStr}</td>
                            <td><strong>${r.horasTotales} hrs</strong></td>
                            <td><strong style="color:#047857;">${r.horasEfectivas} hrs</strong></td>
                            <td>${gpsBadge}</td>
                            <td><span class="badge badge-secondary">${r.estadoJornada}</span></td>
                        </tr>
                    `;
                }).join('')}
            </tbody>
        </table>
    `;
}

function renderReporteBitacora(checadas, container) {
    if (!checadas || checadas.length === 0) {
        container.innerHTML = '<div style="text-align:center; padding:2rem; color:#94a3b8;">No hay marcajes en el periodo seleccionado.</div>';
        return;
    }

    container.innerHTML = `
        <table class="data-table">
            <thead>
                <tr>
                    <th style="text-align:center;">📸 Selfie</th>
                    <th>Fecha y Hora</th>
                    <th>Empleado</th>
                    <th>Tipo</th>
                    <th>Coordenadas GPS</th>
                    <th>Sede de Trabajo Asignada</th>
                    <th>Distancia</th>
                    <th>Estatus</th>
                    <th>Origen</th>
                    <th>Acción</th>
                </tr>
            </thead>
            <tbody>
                ${checadas.map(c => {
                    const dtStr = new Date(c.fechaHora).toLocaleString([], { dateStyle: 'short', timeStyle: 'medium' });
                    const distStr = c.distanciaMetros ? (c.distanciaMetros < 1000 ? `${Math.round(c.distanciaMetros)} m` : `${(c.distanciaMetros/1000).toFixed(2)} km`) : '0 m';
                    
                    let badgeClass = 'badge-success';
                    if (c.estatus === 1) badgeClass = 'badge-warning';
                    if (c.estatus === 2 || !c.enGeocerca) badgeClass = 'badge-danger';

                    return `
                        <tr>
                            <td style="text-align:center;">
                                ${c.fotoSelfieUrl 
                                    ? `<img src="${c.fotoSelfieUrl}" onclick="verSelfieGrande('${c.fotoSelfieUrl}', '${c.empleadoNombre.replace(/'/g, "\\'")}', '${dtStr}', '${c.departamento || ''}', '${distStr}')" style="width:42px; height:42px; border-radius:50%; object-fit:cover; border:2px solid #10b981; cursor:pointer; box-shadow:0 2px 6px rgba(0,0,0,0.15);" title="Clic para ampliar selfie" alt="Selfie" />`
                                    : '<span style="color:#94a3b8; font-size:11px;">--</span>'}
                            </td>
                            <td>${dtStr}</td>
                            <td><strong>${c.empleadoNombre}</strong><br/><small style="color:#64748b;">${c.departamento}</small></td>
                            <td><strong>${c.tipoTexto}</strong></td>
                            <td>${c.latitud ? `${c.latitud.toFixed(5)}, ${c.longitud.toFixed(5)}` : '<span style="color:#94a3b8;">Sin GPS</span>'}</td>
                            <td>${c.sucursalAsignadaNombre || c.sucursalCercanaNombre || 'N/A'}</td>
                            <td>${distStr}</td>
                            <td><span class="badge ${badgeClass}">${c.estatusTexto}</span></td>
                            <td><span class="badge badge-secondary">${c.origen}</span></td>
                            <td style="white-space:nowrap;">
                                ${c.enlaceGoogleMaps ? `<a href="${c.enlaceGoogleMaps}" target="_blank" class="btn btn-secondary btn-sm">🗺️ Mapa</a>` : ''}
                                <button class="btn btn-sm" onclick="borrarChecada('${c.id}')" style="background:#dc2626; color:white; border:none; padding:4px 8px; border-radius:4px; font-weight:bold; margin-left:4px; cursor:pointer;" title="Eliminar registro">🗑️</button>
                            </td>
                        </tr>
                    `;
                }).join('')}
            </tbody>
        </table>
    `;
}

function renderReporteIncidencias(incidencias, container) {
    if (!incidencias || incidencias.length === 0) {
        container.innerHTML = '<div style="text-align:center; padding:2rem; color:#047857; font-weight:600;">🎉 ¡Excelente! No se registran incidencias (retardos ni checadas fuera de zona) en el periodo.</div>';
        return;
    }

    container.innerHTML = `
        <table class="data-table">
            <thead>
                <tr>
                    <th>Fecha y Hora</th>
                    <th>Empleado</th>
                    <th>Incidencia Detectada</th>
                    <th>Detalle de Auditoría GPS / Horario</th>
                    <th>Sede Asignada</th>
                    <th>Ubicación en Mapa</th>
                </tr>
            </thead>
            <tbody>
                ${incidencias.map(c => {
                    const dtStr = new Date(c.fechaHora).toLocaleString([], { dateStyle: 'short', timeStyle: 'medium' });
                    const distStr = c.distanciaMetros ? (c.distanciaMetros < 1000 ? `${Math.round(c.distanciaMetros)} m` : `${(c.distanciaMetros/1000).toFixed(2)} km`) : '0 m';
                    
                    let desc = '';
                    if (c.estatus === 1) {
                        desc = `<span class="badge badge-warning">Retardo: ${c.minutosRetardo} minutos tarde</span>`;
                    }
                    if (!c.enGeocerca || c.estatus === 2) {
                        desc += ` <span class="badge badge-danger">Fuera de Zona GPS (a ${distStr})</span>`;
                    }

                    return `
                        <tr style="background:#fff8f8;">
                            <td>${dtStr}</td>
                            <td><strong>${c.empleadoNombre}</strong> (${c.departamento})</td>
                            <td>${desc}</td>
                            <td>Checó a <strong>${distStr}</strong> de su sede asignada (${c.sucursalAsignadaNombre || c.sucursalCercanaNombre || 'su sede'}). Coordenadas: ${c.latitud?.toFixed(5)}, ${c.longitud?.toFixed(5)}</td>
                            <td>${c.sucursalAsignadaNombre || c.sucursalCercanaNombre || 'N/A'}</td>
                            <td>
                                ${c.enlaceGoogleMaps ? `<a href="${c.enlaceGoogleMaps}" target="_blank" class="btn btn-secondary btn-sm" style="color:#b91c1c;">🗺️ Auditar en Mapa</a>` : ''}
                            </td>
                        </tr>
                    `;
                }).join('')}
            </tbody>
        </table>
    `;
}

function exportarCsv() {
    const fechaInicio = document.getElementById('filtroFechaInicio')?.value || '';
    const fechaFin = document.getElementById('filtroFechaFin')?.value || '';
    const endpoint = currentReportSubtab === 'resumen' 
        ? `/api/reportes/export/csv-resumen?fechaInicio=${fechaInicio}&fechaFin=${fechaFin}`
        : `/api/reportes/export/csv-bitacora?fechaInicio=${fechaInicio}&fechaFin=${fechaFin}`;

    window.open(endpoint, '_blank');
}

// ==========================================
// 6. CATÁLOGOS (EMPLEADOS & SUCURSALES)
// ==========================================
async function loadEmpleados() {
    try {
        const res = await fetch('/api/empleados');
        empleadosCache = await res.json();

        // Llenar selectores
        const previewSelect = document.getElementById('previewEmpSelect');
        const filterSelect = document.getElementById('filtroEmpleado');

        if (previewSelect) {
            previewSelect.innerHTML = empleadosCache.map(e => `
                <option value="${e.id}">${e.nombre} (${e.codigo || e.id})</option>
            `).join('');
            if (empleadosCache.length > 0) {
                actualizarEstadoPreview(empleadosCache[0].id);
                sincronizarGpsPreviewConEmpleado(empleadosCache[0].id);
            }
        }

        if (filterSelect) {
            filterSelect.innerHTML = '<option value="">Todos los empleados</option>' + empleadosCache.map(e => `
                <option value="${e.id}">${e.nombre}</option>
            `).join('');
        }

        renderEmpleadosTable();
    } catch (err) {
        console.error('Error al cargar empleados:', err);
    }
}

function renderEmpleadosTable() {
    const tbody = document.getElementById('empleadosTableBody');
    if (!tbody) return;

    if (!empleadosCache || empleadosCache.length === 0) {
        tbody.innerHTML = '<tr><td colspan="10" style="text-align:center; padding:2rem; color:#94a3b8;">No hay colaboradores registrados. Haz clic en "+ Nuevo Empleado" para agregar uno.</td></tr>';
        return;
    }

    tbody.innerHTML = empleadosCache.map(e => {
        const suc = sucursalesCache.find(s => s.id === e.sucursalId);
        return `
            <tr>
                <td><strong>${e.codigo || e.id}</strong></td>
                <td><strong>${e.nombre}</strong></td>
                <td><span style="color:#047857;">${e.telefono || '--'}</span></td>
                <td>${e.departamento}</td>
                <td>${e.puesto}</td>
                <td>${suc ? suc.nombre : '<span style="color:#94a3b8;">Cualquiera</span>'}</td>
                <td>${e.horaEntrada} - ${e.horaSalida} <small style="color:#64748b;">(${e.toleranciaMinutos}m tol.)</small></td>
                <td>${e.permitirRemoto ? '<span class="badge badge-info">Sí (Remoto)</span>' : '<span class="badge badge-secondary">Solo Sede</span>'}</td>
                <td>${e.activo ? '<span class="badge badge-success">Activo</span>' : '<span class="badge badge-danger">Inactivo</span>'}</td>
                <td style="white-space:nowrap;">
                    <button class="btn btn-secondary btn-sm" onclick="editarEmpleado('${e.id}')">✏️ Editar</button>
                    <button class="btn btn-sm" onclick="borrarEmpleado('${e.id}', '${e.nombre.replace(/'/g, "\\'")}')" style="background:#dc2626; color:white; border:none; padding:4px 8px; border-radius:4px; font-weight:bold; margin-left:4px; cursor:pointer;" title="Eliminar colaborador">🗑️ Borrar</button>
                </td>
            </tr>
        `;
    }).join('');
}

async function loadSucursales() {
    try {
        const res = await fetch('/api/sucursales');
        sucursalesCache = await res.json();
        renderSucursalesTable();
    } catch (err) {
        console.error('Error al cargar sucursales:', err);
    }
}

function renderSucursalesTable() {
    const tbody = document.getElementById('sucursalesTableBody');
    if (!tbody) return;

    if (!sucursalesCache || sucursalesCache.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" style="text-align:center; padding:2rem; color:#94a3b8;">No hay sucursales registradas. Haz clic en "+ Nueva Sucursal" para agregar una.</td></tr>';
        return;
    }

    tbody.innerHTML = sucursalesCache.map(s => {
        return `
            <tr>
                <td><strong>${s.id}</strong></td>
                <td><span style="display:inline-block; width:12px; height:12px; border-radius:50%; background:${s.colorHex}; margin-right:6px;"></span><strong>${s.nombre}</strong></td>
                <td>${s.direccion}</td>
                <td>${s.latitud.toFixed(5)}, ${s.longitud.toFixed(5)}</td>
                <td><strong>${s.radioMetros} m</strong></td>
                <td>${s.activa ? '<span class="badge badge-success">Activa</span>' : '<span class="badge badge-secondary">Inactiva</span>'}</td>
                <td style="white-space:nowrap;">
                    <button class="btn btn-secondary btn-sm" onclick="editarSucursal('${s.id}')">✏️ Editar</button>
                    <button class="btn btn-sm" onclick="borrarSucursal('${s.id}', '${s.nombre.replace(/'/g, "\\'")}')" style="background:#dc2626; color:white; border:none; padding:4px 8px; border-radius:4px; font-weight:bold; margin-left:4px; cursor:pointer;" title="Eliminar sucursal">🗑️ Borrar</button>
                    <a href="https://www.google.com/maps?q=${s.latitud},${s.longitud}" target="_blank" class="btn btn-secondary btn-sm" style="margin-left:4px;">🗺️ Ver</a>
                </td>
            </tr>
        `;
    }).join('');
}

// Modales CRUD Empleados
function abrirModalEmpleado(emp = null) {
    document.getElementById('modalEmpleadoTitle').textContent = emp ? 'Editar Empleado' : 'Nuevo Empleado';
    document.getElementById('empId').value = emp ? emp.id : '';
    document.getElementById('empCodigo').value = emp ? (emp.codigo || emp.id) : '';
    document.getElementById('empPin').value = emp ? (emp.pin || '1234') : '1234';
    document.getElementById('empNombre').value = emp ? emp.nombre : '';
    document.getElementById('empTelefono').value = emp ? emp.telefono : '';
    document.getElementById('empDepto').value = emp ? emp.departamento : 'Operaciones';
    document.getElementById('empPuesto').value = emp ? emp.puesto : 'Colaborador';
    document.getElementById('empHoraEntrada').value = emp ? emp.horaEntrada : '08:00';
    document.getElementById('empHoraSalida').value = emp ? emp.horaSalida : '17:00';
    document.getElementById('empTolerancia').value = emp ? emp.toleranciaMinutos : 15;
    document.getElementById('empPermitirRemoto').checked = emp ? emp.permitirRemoto : false;

    const sucSelect = document.getElementById('empSucursal');
    sucSelect.innerHTML = '<option value="">Cualquier sucursal / Sin asignar</option>' + sucursalesCache.map(s => `
        <option value="${s.id}" ${emp && emp.sucursalId === s.id ? 'selected' : ''}>${s.nombre}</option>
    `).join('');

    document.getElementById('modalEmpleado').classList.add('active');
}

function cerrarModalEmpleado() {
    document.getElementById('modalEmpleado').classList.remove('active');
}

async function guardarEmpleado(e) {
    e.preventDefault();
    const id = document.getElementById('empId').value;
    const nuevo = {
        id: id || undefined,
        codigo: document.getElementById('empCodigo').value.trim(),
        pin: document.getElementById('empPin').value.trim(),
        nombre: document.getElementById('empNombre').value.trim(),
        telefono: document.getElementById('empTelefono').value.trim(),
        departamento: document.getElementById('empDepto').value.trim(),
        puesto: document.getElementById('empPuesto').value.trim(),
        sucursalId: document.getElementById('empSucursal').value || null,
        horaEntrada: document.getElementById('empHoraEntrada').value,
        horaSalida: document.getElementById('empHoraSalida').value,
        toleranciaMinutos: parseInt(document.getElementById('empTolerancia').value) || 15,
        permitirRemoto: document.getElementById('empPermitirRemoto').checked,
        activo: true
    };

    try {
        await fetch('/api/empleados', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(nuevo)
        });
        cerrarModalEmpleado();
        await loadEmpleados();
        alert('Empleado guardado con éxito.');
    } catch (err) {
        alert('Error al guardar empleado.');
    }
}

function editarEmpleado(id) {
    const emp = empleadosCache.find(e => e.id === id);
    if (emp) abrirModalEmpleado(emp);
}

// Modales CRUD Sucursales
function abrirModalSucursal(suc = null) {
    document.getElementById('modalSucursalTitle').textContent = suc ? 'Editar Sucursal' : 'Nueva Sucursal';
    document.getElementById('sucId').value = suc ? suc.id : '';
    document.getElementById('sucNombre').value = suc ? suc.nombre : '';
    document.getElementById('sucDireccion').value = suc ? suc.direccion : '';
    document.getElementById('sucLatitud').value = suc ? suc.latitud : 19.429031;
    document.getElementById('sucLongitud').value = suc ? suc.longitud : -99.162458;
    document.getElementById('sucRadio').value = suc ? suc.radioMetros : 200;
    document.getElementById('sucColor').value = suc ? suc.colorHex : '#2563EB';

    document.getElementById('modalSucursal').classList.add('active');
}

function cerrarModalSucursal() {
    document.getElementById('modalSucursal').classList.remove('active');
}

function capturarGpsParaSucursal() {
    if (!navigator.geolocation) {
        alert('Tu navegador no soporta geolocalización satelital.');
        return;
    }
    navigator.geolocation.getCurrentPosition(
        pos => {
            document.getElementById('sucLatitud').value = pos.coords.latitude.toFixed(6);
            document.getElementById('sucLongitud').value = pos.coords.longitude.toFixed(6);
            alert(`📍 Coordenadas GPS fijadas con éxito:\nLat: ${pos.coords.latitude.toFixed(6)}\nLon: ${pos.coords.longitude.toFixed(6)}`);
        },
        err => {
            alert('No se pudo obtener la posición GPS del navegador. Asegúrate de permitir el permiso de ubicación en tu navegador.');
        },
        { enableHighAccuracy: true, timeout: 8000 }
    );
}

async function guardarSucursal(e) {
    e.preventDefault();
    const id = document.getElementById('sucId').value;
    let latRaw = document.getElementById('sucLatitud').value.toString().trim().replace(',', '.');
    let lonRaw = document.getElementById('sucLongitud').value.toString().trim().replace(',', '.');
    let lat = parseFloat(latRaw);
    let lon = parseFloat(lonRaw);

    if (isNaN(lat) || isNaN(lon)) {
        alert('Por favor introduce coordenadas numéricas válidas para la sucursal.');
        return;
    }

    // Auto-corregir si se ingresó sin punto decimal (ej: -101921107 -> -101.921107)
    while (Math.abs(lat) > 90) lat /= 10.0;
    while (Math.abs(lon) > 180) lon /= 10.0;

    const nueva = {
        id: id || undefined,
        nombre: document.getElementById('sucNombre').value.trim(),
        direccion: document.getElementById('sucDireccion').value.trim(),
        latitud: parseFloat(lat.toFixed(6)),
        longitud: parseFloat(lon.toFixed(6)),
        radioMetros: parseFloat(document.getElementById('sucRadio').value) || 200,
        colorHex: document.getElementById('sucColor').value || '#2563EB',
        activa: true
    };

    try {
        await fetch('/api/sucursales', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(nueva)
        });
        cerrarModalSucursal();
        await loadSucursales();
        await loadDashboard(true);
        alert('Sucursal guardada con éxito.');
    } catch (err) {
        alert('Error al guardar sucursal.');
    }
}

function editarSucursal(id) {
    const suc = sucursalesCache.find(s => s.id === id);
    if (suc) abrirModalSucursal(suc);
}

// ==========================================
// 7. CONFIGURACIÓN DEL SISTEMA
// ==========================================
async function loadConfiguracion() {
    try {
        const res = await fetch('/api/configuracion');
        const config = await res.json();

        document.getElementById('cfgNombreEmpresa').value = config.nombreEmpresa || '';
        document.getElementById('cfgTolerancia').value = config.toleranciaMinutosDefault || 15;
        document.getElementById('cfgRadio').value = config.radioGeocercaDefaultMetros || 200;
        document.getElementById('cfgPinAdmin').value = config.pinAdmin || '1234';
        document.getElementById('cfgServidorUrl').value = config.servidorUrl || window.location.origin;
        if (document.getElementById('cfgServidorDatosMovilesUrl')) {
            document.getElementById('cfgServidorDatosMovilesUrl').value = config.servidorDatosMovilesUrl || '';
        }

        const badgeMovil = document.getElementById('badgeDatosMoviles');
        if (badgeMovil) {
            if (config.servidorDatosMovilesUrl) {
                badgeMovil.style.display = 'inline-flex';
                badgeMovil.dataset.url = config.servidorDatosMovilesUrl;
                badgeMovil.title = `Túnel Activo: ${config.servidorDatosMovilesUrl} (Clic para copiar)`;
            } else {
                badgeMovil.style.display = 'none';
            }
        }
    } catch (err) {
        console.error('Error al cargar config:', err);
    }
}

function copiarUrlDatosMoviles() {
    const badge = document.getElementById('badgeDatosMoviles');
    const url = badge?.dataset?.url || document.getElementById('cfgServidorDatosMovilesUrl')?.value;
    if (url) {
        navigator.clipboard.writeText(url).then(() => {
            alert(`✅ URL de Datos Móviles copiada al portapapeles:\n\n${url}\n\nPégala en la app GeoCheck de los celulares en [Config Servidor].`);
        }).catch(() => {
            prompt('Copia esta URL para los celulares con datos móviles:', url);
        });
    } else {
        alert('No hay una URL de datos móviles activa. Iníciala con el archivo iniciar_datos_moviles.bat');
    }
}

async function guardarConfiguracion(e) {
    e.preventDefault();
    const config = {
        nombreEmpresa: document.getElementById('cfgNombreEmpresa').value,
        toleranciaMinutosDefault: parseInt(document.getElementById('cfgTolerancia').value) || 15,
        radioGeocercaDefaultMetros: parseFloat(document.getElementById('cfgRadio').value) || 200,
        pinAdmin: document.getElementById('cfgPinAdmin').value,
        servidorUrl: document.getElementById('cfgServidorUrl').value,
        servidorDatosMovilesUrl: document.getElementById('cfgServidorDatosMovilesUrl')?.value || ''
    };

    try {
        await fetch('/api/configuracion', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(config)
        });
        alert('Parámetros guardados correctamente.');
    } catch (err) {
        alert('Error al guardar configuración.');
    }
}

// ==========================================
// FUNCIONES DE ELIMINACIÓN Y MANTENIMIENTO
// ==========================================

async function borrarSucursal(id, nombre) {
    if (!confirm(`¿Estás seguro de que deseas eliminar permanentemente la sucursal "${nombre}"?`)) return;
    try {
        const res = await fetch(`/api/sucursales/${id}`, { method: 'DELETE' });
        const data = await res.json();
        if (data.success) {
            alert(`Sucursal "${nombre}" eliminada correctamente.`);
            await loadSucursales();
            await loadDashboard(true);
        } else {
            alert('Error al eliminar sucursal.');
        }
    } catch (e) {
        alert('Error de conexión al eliminar sucursal.');
    }
}

async function borrarEmpleado(id, nombre) {
    if (!confirm(`¿Estás seguro de que deseas eliminar permanentemente al colaborador "${nombre}"?`)) return;
    try {
        const res = await fetch(`/api/empleados/${id}`, { method: 'DELETE' });
        const data = await res.json();
        if (data.success) {
            alert(`Colaborador "${nombre}" eliminado correctamente.`);
            await loadEmpleados();
            await loadDashboard(true);
        } else {
            alert('Error al eliminar colaborador.');
        }
    } catch (e) {
        alert('Error de conexión al eliminar colaborador.');
    }
}

async function borrarChecada(id) {
    if (!confirm('¿Deseas eliminar este registro de asistencia?')) return;
    try {
        const res = await fetch(`/api/checadas/${id}`, { method: 'DELETE' });
        const data = await res.json();
        if (data.success) {
            await loadDashboard(true);
            if (document.getElementById('reportes')?.classList.contains('active')) {
                filtrarReporte();
            }
        } else {
            alert('Error al eliminar registro.');
        }
    } catch (e) {
        alert('Error de conexión al eliminar registro.');
    }
}

async function borrarTodosLosRegistros() {
    if (!confirm('⚠️ ¿Estás seguro de que deseas ELIMINAR TODOS los registros de asistencia?\n\nEsta acción borrará todo el historial de checadas acumuladas pero conservará tus colaboradores y sucursales intactos.')) return;
    try {
        const res = await fetch('/api/checadas', { method: 'DELETE' });
        const data = await res.json();
        if (data.success) {
            alert('✅ Todos los registros de asistencia han sido eliminados correctamente.');
            await loadDashboard(true);
            if (document.getElementById('reportes')?.classList.contains('active')) {
                filtrarReporte();
            }
        } else {
            alert('Error al eliminar los registros.');
        }
    } catch (e) {
        alert('Error de conexión al eliminar los registros.');
    }
}

async function limpiarTodaLaBaseDeDatos() {
    const c1 = confirm('🚨 ADVERTENCIA DE VACIADO TOTAL 🚨\n\n¿Estás seguro de que deseas ELIMINAR TODA LA BASE DE DATOS?\n\nSe eliminarán permanentemente:\n- Todas las sucursales\n- Todos los colaboradores\n- Todos los registros de checadas\n\nLa aplicación quedará completamente limpia y en blanco para tu empresa.');
    if (!c1) return;

    const palabra = prompt('Para confirmar que deseas dejar LIMPIA la aplicación, escribe la palabra:\nBORRAR');
    if (palabra !== 'BORRAR') {
        alert('Acción cancelada. No se modificó ningún dato.');
        return;
    }

    try {
        const res = await fetch('/api/sistema/limpiar-todo', { method: 'POST' });
        const data = await res.json();
        if (data.success) {
            alert('✅ ¡Base de datos completamente vaciada!\nLa aplicación está limpia y lista para que registres tus propias sucursales y colaboradores.');
            window.location.reload();
        } else {
            alert('Error al limpiar la base de datos.');
        }
    } catch (e) {
        alert('Error de conexión al limpiar la base de datos.');
    }
}

async function restaurarDatosDemo() {
    if (!confirm('¿Deseas restaurar las sucursales, colaboradores y checadas de ejemplo?')) return;
    try {
        const res = await fetch('/api/sistema/restaurar-demo', { method: 'POST' });
        const data = await res.json();
        if (data.success) {
            alert('✅ Datos de demostración restaurados correctamente.');
            window.location.reload();
        } else {
            alert('Error al restaurar datos de demostración.');
        }
    } catch (e) {
        alert('Error de conexión al restaurar datos.');
    }
}

// ==========================================
// VISUALIZACIÓN Y CAPTURA DE SELFIES
// ==========================================

function verSelfieGrande(url, nombre, fecha, depto, dist) {
    const modal = document.getElementById('modalSelfie');
    if (!modal) return;

    document.getElementById('modalSelfieImg').src = url;
    document.getElementById('modalSelfieTitle').textContent = `📸 Verificación Facial: ${nombre}`;
    document.getElementById('modalSelfieDetalle').innerHTML = `
        <div style="margin-bottom:4px;"><strong>Colaborador:</strong> ${nombre}</div>
        <div style="margin-bottom:4px;"><strong>Departamento:</strong> ${depto || 'General'}</div>
        <div style="margin-bottom:4px;"><strong>Fecha / Hora:</strong> ${fecha}</div>
        <div style="margin-bottom:4px;"><strong>Distancia a Sede:</strong> ${dist || 'N/A'}</div>
        <div style="margin-top:8px; padding-top:6px; border-top:1px dashed #cbd5e1; color:#047857; font-weight:bold;">
            🟢 Selfie capturada por el teléfono móvil al registrar la Entrada.
        </div>
    `;
    modal.classList.add('active');
}

function cerrarModalSelfie() {
    const modal = document.getElementById('modalSelfie');
    if (modal) modal.classList.remove('active');
}

async function capturarSelfieWeb(nombre) {
    try {
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user', width: 320, height: 320 } });
            const video = document.createElement('video');
            video.srcObject = stream;
            await video.play();

            const canvas = document.createElement('canvas');
            canvas.width = 300;
            canvas.height = 300;
            const ctx = canvas.getContext('2d');
            ctx.drawImage(video, 0, 0, 300, 300);

            stream.getTracks().forEach(t => t.stop());
            return canvas.toDataURL('image/jpeg', 0.82);
        }
    } catch (e) {
        console.warn('Cámara web no disponible en navegador, generando selfie sintética:', e);
    }

    // Fallback si la PC no tiene webcam: genera avatar con firma digital
    const canvas = document.createElement('canvas');
    canvas.width = 300;
    canvas.height = 300;
    const ctx = canvas.getContext('2d');

    const grad = ctx.createLinearGradient(0, 0, 300, 300);
    grad.addColorStop(0, '#047857');
    grad.addColorStop(1, '#0f172a');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, 300, 300);

    ctx.fillStyle = 'rgba(255, 255, 255, 0.2)';
    ctx.beginPath();
    ctx.arc(150, 115, 55, 0, Math.PI * 2);
    ctx.fill();

    ctx.font = '45px sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('👤', 150, 130);

    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 15px sans-serif';
    ctx.fillText(nombre, 150, 205);

    ctx.fillStyle = '#a7f3d0';
    ctx.font = '12px sans-serif';
    ctx.fillText('📸 Selfie de Verificación Facial', 150, 230);

    ctx.fillStyle = '#94a3b8';
    ctx.font = '10px sans-serif';
    ctx.fillText(new Date().toLocaleString(), 150, 255);

    return canvas.toDataURL('image/jpeg', 0.85);
}
