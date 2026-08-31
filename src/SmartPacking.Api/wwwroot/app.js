const typeNames = ['Camiseta', 'Pantalón', 'Pantalón corto', 'Chaqueta', 'Zapatillas', 'Sandalias', 'Accesorio'];
const checklistCategories = ['Documentos', 'Aseo', 'Tecnología', 'Salud', 'Otros'];
const tripSelect = document.querySelector('#trip-select');
const feedback = document.querySelector('#feedback');
const tripForm = document.querySelector('#trip-form');
const clothingForm = document.querySelector('#clothing-form');
const checklistForm = document.querySelector('#checklist-form');
const usageForm = document.querySelector('#usage-form');
const profileForm = document.querySelector('#profile-form');
const profileSelect = document.querySelector('#profile-select');
let activeTripId, activeProfileId, editingItemId, editingItem, currentPlan, familyProfiles = [];

function showFeedback(message, isError = false) { feedback.textContent = message; feedback.hidden = false; feedback.classList.toggle('error', isError); }
async function json(response) { if (!response.ok) throw new Error(); return response.json(); }

function openClothingForm(item) {
  editingItemId = item?.id; editingItem = item; clothingForm.reset();
  document.querySelector('#clothing-form-title').textContent = item ? 'Editar prenda' : 'Añadir al armario';
  document.querySelector('#save-clothing').textContent = item ? 'Guardar cambios' : 'Añadir prenda';
  if (item) { for (const field of ['name', 'type', 'season', 'style', 'color', 'warmthLevel', 'weightGrams', 'preferenceScore', 'ownerProfileId']) clothingForm.elements[field].value = item[field] ?? ''; clothingForm.elements.waterproof.checked = item.waterproof; }
  clothingForm.hidden = false;
}
async function uploadPhoto(id, photo) {
  if (!(photo instanceof File) || photo.size === 0) return true;
  const data = new FormData(); data.append('photo', photo);
  const response = await fetch(`/api/wardrobe/${id}/photo`, { method: 'POST', body: data });
  if (!response.ok) showFeedback('La prenda se ha guardado, pero no se ha podido subir la foto JPEG.', true);
  return response.ok;
}
async function loadTrips(selectedId) {
  const trips = await json(await fetch('/api/trips'));
  tripSelect.replaceChildren(...trips.map(trip => new Option(`${trip.destination} · ${trip.startDate}`, trip.id)));
  activeTripId = selectedId ?? activeTripId ?? trips[0]?.id; tripSelect.value = activeTripId;
}
async function loadWardrobe() {
  const wardrobe = await json(await fetch('/api/wardrobe')); const container = document.querySelector('#wardrobe-items'); const template = document.querySelector('#wardrobe-template');
  container.replaceChildren(); document.querySelector('#wardrobe-count').textContent = `${wardrobe.length} prendas`;
  for (const item of wardrobe) {
    const entry = template.content.cloneNode(true); entry.querySelector('strong').textContent = item.name; const owner = familyProfiles.find(profile => profile.id === item.ownerProfileId)?.name ?? 'Compartida'; entry.querySelector('.wardrobe-meta').textContent = `${typeNames[item.type]} · ${item.color} · ${owner}`;
    const photo = entry.querySelector('.clothing-photo'); photo.src = `/uploads/${item.id}.jpg`; photo.alt = item.name; photo.addEventListener('error', () => photo.hidden = true, { once: true });
    const clean = entry.querySelector('.clean'), available = entry.querySelector('.available'); clean.checked = item.isClean; available.checked = item.isAvailable;
    const save = async () => { const response = await fetch(`/api/wardrobe/${item.id}/status`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ isClean: clean.checked, isAvailable: available.checked }) }); if (!response.ok) return showFeedback('No se ha podido actualizar la prenda.', true); showFeedback(`${item.name} actualizada.`); await refreshTripData(); };
    clean.addEventListener('change', save); available.addEventListener('change', save); entry.querySelector('.edit-clothing').addEventListener('click', () => openClothingForm(item));
    entry.querySelector('.delete-clothing').addEventListener('click', async () => {
      if (!confirm(`¿Eliminar “${item.name}”? Se quitará también de las maletas donde aparezca.`)) return;
      try { const response = await fetch(`/api/wardrobe/${item.id}`, { method: 'DELETE' }); if (!response.ok) throw new Error(); await loadWardrobe(); await refreshTripData(); showFeedback(`${item.name} eliminada.`); } catch { showFeedback('No se ha podido eliminar la prenda.', true); }
    }); container.append(entry);
  }
  const deleted = await json(await fetch('/api/wardrobe/deleted')); const deletedContainer = document.querySelector('#deleted-items'); deletedContainer.replaceChildren();
  for (const item of deleted) { const entry = document.createElement('article'); entry.className = 'wardrobe-item'; const name = document.createElement('strong'); name.textContent = item.name; const restore = document.createElement('button'); restore.type = 'button'; restore.textContent = 'Recuperar'; restore.addEventListener('click', async () => { const response = await fetch(`/api/wardrobe/${item.id}/restore`, { method: 'POST' }); if (!response.ok) return showFeedback('No se ha podido recuperar la prenda.', true); await loadWardrobe(); await refreshTripData(); showFeedback(`${item.name} recuperada.`); }); entry.append(name, restore); deletedContainer.append(entry); }
}
async function loadWeather() {
  const detail = document.querySelector('#weather-detail');
  try { const value = await json(await fetch(`/api/trips/${activeTripId}/weather`)); document.querySelector('#temperature').textContent = `${value.minimumTemperatureCelsius}–${value.maximumTemperatureCelsius} °C`; detail.textContent = value.maximumPrecipitationProbability > 30 ? `Lluvia: hasta ${value.maximumPrecipitationProbability}%` : 'Previsión actualizada'; }
  catch { detail.textContent = 'Previsión disponible hasta 16 días antes'; }
}
async function loadProfiles() {
  const [profiles, travellers] = await Promise.all([json(await fetch('/api/profiles')), json(await fetch(`/api/trips/${activeTripId}/profiles`))]); familyProfiles = profiles;
  const selected = new Set(travellers.map(profile => profile.id)); const container = document.querySelector('#profile-items'); container.replaceChildren();
  for (const profile of profiles) { const label = document.createElement('label'); label.className = 'profile-item'; const box = document.createElement('input'); box.type = 'checkbox'; box.value = profile.id; box.checked = selected.has(profile.id); label.append(box, ` ${profile.name}`); container.append(label); }
  activeProfileId = activeProfileId && selected.has(activeProfileId) ? activeProfileId : travellers[0]?.id;
  profileSelect.replaceChildren(...travellers.map(profile => new Option(profile.name, profile.id))); profileSelect.value = activeProfileId;
  document.querySelector('#clothing-owner').replaceChildren(...profiles.map(profile => new Option(profile.name, profile.id)));
}
async function loadPlan() {
  const response = await json(await fetch(`/api/trips/${activeTripId}/profiles/${activeProfileId}/packing-list`)); currentPlan = response.plan; const { trip, items, totalWeightGrams, packingListId } = currentPlan;
  document.querySelector('#packing-title').textContent = `Maleta de ${response.profile.name}`;
  document.querySelector('#trip').textContent = `${trip.destination} · ${trip.days} días · ${trip.startDate} al ${trip.endDate}`;
  document.querySelector('#temperature').textContent = `${trip.minimumTemperatureCelsius}–${trip.maximumTemperatureCelsius} °C`;
  document.querySelector('#items').textContent = items.length; document.querySelector('#weight').textContent = `${(totalWeightGrams / 1000).toFixed(1)} kg`;
  document.querySelector('#outfits').textContent = Math.max(1, items.filter(x => x.recommendation.item.type === 0).length * items.filter(x => x.recommendation.item.type === 1 || x.recommendation.item.type === 2).length);
  const container = document.querySelector('#recommendations'), template = document.querySelector('#item-template'); container.replaceChildren();
  for (const planned of items) { const recommendation = planned.recommendation, card = template.content.cloneNode(true), box = card.querySelector('input'); box.checked = planned.isPacked; box.addEventListener('change', async () => { const response = await fetch(`/api/profile-packing-lists/${packingListId}/items/${recommendation.item.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ isPacked: box.checked }) }); if (!response.ok) showFeedback('No se ha podido actualizar la maleta.', true); }); card.querySelector('.type').textContent = typeNames[recommendation.item.type]; card.querySelector('h3').textContent = recommendation.item.name; card.querySelector('.reason').textContent = recommendation.reasons.join(' · '); card.querySelector('.score b').textContent = recommendation.score; container.append(card); }
}
async function loadChecklist() {
  const items = await json(await fetch(`/api/trips/${activeTripId}/checklist`)); const container = document.querySelector('#checklist-items'), template = document.querySelector('#checklist-template'); container.replaceChildren();
  for (const item of items) { const entry = template.content.cloneNode(true), box = entry.querySelector('input'), label = entry.querySelector('span'); box.checked = item.isPacked; label.textContent = item.name; label.dataset.category = checklistCategories[item.category]; box.addEventListener('change', async () => { const response = await fetch(`/api/checklist/${item.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ isPacked: box.checked }) }); if (!response.ok) showFeedback('No se ha podido actualizar la checklist.', true); }); container.append(entry); }
}
async function loadUsage() {
  const saved = await json(await fetch(`/api/trips/${activeTripId}/usage`)); const usedIds = new Set(saved.filter(item => item.wasUsed).map(item => item.clothingItemId)); const container = document.querySelector('#usage-items'), template = document.querySelector('#usage-template'); container.replaceChildren();
  for (const planned of currentPlan.items) { const entry = template.content.cloneNode(true), item = planned.recommendation.item, box = entry.querySelector('input'); box.value = item.id; box.checked = usedIds.has(item.id); entry.querySelector('span').textContent = item.name; container.append(entry); }
}
async function refreshTripData() { await loadProfiles(); await loadPlan(); await Promise.all([loadWeather(), loadChecklist(), loadUsage()]); }

document.querySelector('#pack-all').addEventListener('click', () => document.querySelectorAll('#recommendations input:not(:checked)').forEach(input => input.click()));
tripSelect.addEventListener('change', async () => { activeTripId = tripSelect.value; await refreshTripData(); });
profileSelect.addEventListener('change', async () => { activeProfileId = profileSelect.value; await loadPlan(); await loadUsage(); });
document.querySelector('#show-trip-form').addEventListener('click', () => tripForm.hidden = !tripForm.hidden);
document.querySelector('#show-clothing-form').addEventListener('click', () => openClothingForm());
document.querySelector('#delete-trip').addEventListener('click', async () => {
  const name = tripSelect.options[tripSelect.selectedIndex]?.text;
  if (!activeTripId || !confirm(`¿Eliminar el viaje “${name}”? También se eliminarán sus maletas, checklist y seguimiento.`)) return;
  try { const response = await fetch(`/api/trips/${activeTripId}`, { method: 'DELETE' }); if (!response.ok) throw new Error(); activeTripId = undefined; activeProfileId = undefined; await loadTrips(); await refreshTripData(); showFeedback('Viaje eliminado.'); } catch { showFeedback('No se ha podido eliminar el viaje.', true); }
});
tripForm.addEventListener('submit', async event => { event.preventDefault(); const form = new FormData(tripForm), trip = Object.fromEntries(form); trip.minimumTemperatureCelsius = Number(trip.minimumTemperatureCelsius); trip.maximumTemperatureCelsius = Number(trip.maximumTemperatureCelsius); trip.activities = [...tripForm.querySelectorAll('input[name="activities"]:checked')].map(input => Number(input.value)); try { const created = await json(await fetch('/api/trips', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(trip) })); tripForm.reset(); tripForm.hidden = true; await loadTrips(created.id); await refreshTripData(); showFeedback(`Viaje a ${created.destination} creado.`); } catch { showFeedback('No se ha podido crear el viaje. Revisa los datos.', true); } });
clothingForm.addEventListener('submit', async event => { event.preventDefault(); const form = new FormData(clothingForm), item = Object.fromEntries(form), photo = item.photo; delete item.photo; Object.assign(item, { id: editingItemId ?? '00000000-0000-0000-0000-000000000000', type: Number(item.type), season: Number(item.season), warmthLevel: Number(item.warmthLevel), style: Number(item.style), weightGrams: Number(item.weightGrams), waterproof: clothingForm.elements.waterproof.checked, isClean: editingItem?.isClean ?? true, isAvailable: editingItem?.isAvailable ?? true, preferenceScore: Number(item.preferenceScore), combinesWith: editingItem?.combinesWith ?? [] }); try { const saved = await json(await fetch(editingItemId ? `/api/wardrobe/${editingItemId}` : '/api/wardrobe', { method: editingItemId ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(item) })); const photoSaved = await uploadPhoto(saved.id, photo); clothingForm.reset(); clothingForm.hidden = true; editingItemId = undefined; editingItem = undefined; await loadWardrobe(); await refreshTripData(); if (photoSaved) showFeedback(`${saved.name} se ha guardado.`); } catch { showFeedback('No se ha podido guardar la prenda. Comprueba sus datos.', true); } });
checklistForm.addEventListener('submit', async event => { event.preventDefault(); const form = new FormData(checklistForm); try { await json(await fetch(`/api/trips/${activeTripId}/checklist`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: form.get('name'), category: Number(form.get('category')) }) })); checklistForm.reset(); await loadChecklist(); showFeedback('Elemento añadido a la checklist.'); } catch { showFeedback('No se ha podido añadir el elemento.', true); } });
profileForm.addEventListener('submit', async event => { event.preventDefault(); const form = new FormData(profileForm); try { await json(await fetch('/api/profiles', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: form.get('name') }) })); profileForm.reset(); await loadProfiles(); showFeedback('Perfil añadido. Selecciónalo como viajero para crear su maleta.'); } catch { showFeedback('No se ha podido añadir el perfil.', true); } });
document.querySelector('#save-travellers').addEventListener('click', async () => { const profileIds = [...document.querySelectorAll('#profile-items input:checked')].map(input => input.value); if (profileIds.length === 0) return showFeedback('Selecciona al menos un viajero.', true); try { const response = await fetch(`/api/trips/${activeTripId}/profiles`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ profileIds }) }); if (!response.ok) throw new Error(); activeProfileId = profileIds[0]; await refreshTripData(); showFeedback('Viajeros guardados: cada uno tiene su propia maleta.'); } catch { showFeedback('No se han podido guardar los viajeros.', true); } });
usageForm.addEventListener('submit', async event => { event.preventDefault(); const used = new Set([...document.querySelectorAll('#usage-items input:checked')].map(input => input.value)); const data = currentPlan.items.map(planned => ({ tripId: activeTripId, clothingItemId: planned.recommendation.item.id, wasUsed: used.has(planned.recommendation.item.id) })); try { const response = await fetch(`/api/trips/${activeTripId}/usage`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) }); if (!response.ok) throw new Error(); showFeedback('Uso real guardado para este viaje.'); } catch { showFeedback('No se ha podido guardar el seguimiento.', true); } });

document.querySelectorAll('.tab').forEach(tab => tab.addEventListener('click', () => { const view = tab.dataset.tab; document.querySelectorAll('.tab').forEach(button => button.classList.toggle('active', button === tab)); document.querySelectorAll('[data-view]').forEach(section => section.hidden = section.dataset.view !== view); }));
document.querySelector('.tab.active').click();

try { await loadTrips(); await loadWardrobe(); await refreshTripData(); } catch { showFeedback('No se han podido cargar los datos de la aplicación.', true); }
