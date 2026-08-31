const typeNames = ['Camiseta', 'Pantalón', 'Pantalón corto', 'Chaqueta', 'Zapatillas', 'Sandalias', 'Accesorio'];
const tripSelect = document.querySelector('#trip-select');
const feedback = document.querySelector('#feedback');
let activeTripId;

function showFeedback(message, isError = false) {
  feedback.textContent = message;
  feedback.hidden = false;
  feedback.classList.toggle('error', isError);
}

async function loadTrips(selectTripId) {
  const trips = await (await fetch('/api/trips')).json();
  tripSelect.replaceChildren(...trips.map(trip => new Option(`${trip.destination} · ${trip.startDate}`, trip.id)));
  activeTripId = selectTripId ?? activeTripId ?? trips[0]?.id;
  tripSelect.value = activeTripId;
}

async function loadWardrobe() {
  const wardrobe = await (await fetch('/api/wardrobe')).json();
  const container = document.querySelector('#wardrobe-items');
  const template = document.querySelector('#wardrobe-template');
  container.replaceChildren();
  document.querySelector('#wardrobe-count').textContent = `${wardrobe.length} prendas`;
  for (const item of wardrobe) {
    const entry = template.content.cloneNode(true);
    entry.querySelector('strong').textContent = item.name;
    entry.querySelector('.wardrobe-meta').textContent = `${typeNames[item.type]} · ${item.color}`;
    const clean = entry.querySelector('.clean'); const available = entry.querySelector('.available');
    clean.checked = item.isClean; available.checked = item.isAvailable;
    const saveStatus = async () => {
      const response = await fetch(`/api/wardrobe/${item.id}/status`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ isClean: clean.checked, isAvailable: available.checked }) });
      if (!response.ok) { showFeedback('No se ha podido actualizar la prenda.', true); return; }
      showFeedback(`${item.name} actualizada.`); await loadPlan();
    };
    clean.addEventListener('change', saveStatus); available.addEventListener('change', saveStatus);
    container.append(entry);
  }
}

async function loadPlan() {
  const response = await fetch(`/api/trips/${activeTripId}/packing-list`);
  const recommendation = await response.json();
const { trip, items, totalWeightGrams, packingListId } = recommendation;
document.querySelector('#trip').textContent = `${trip.destination} · ${trip.days} días · ${trip.startDate} al ${trip.endDate}`;
document.querySelector('#temperature').textContent = `${trip.minimumTemperatureCelsius}–${trip.maximumTemperatureCelsius} °C`;
document.querySelector('#items').textContent = items.length;
document.querySelector('#weight').textContent = `${(totalWeightGrams / 1000).toFixed(1)} kg`;
document.querySelector('#outfits').textContent = Math.max(1, items.filter(x => x.recommendation.item.type === 0).length * items.filter(x => x.recommendation.item.type === 1 || x.recommendation.item.type === 2).length);
const container = document.querySelector('#recommendations');
container.replaceChildren();
const template = document.querySelector('#item-template');
for (const plannedItem of items) {
  const recommendationItem = plannedItem.recommendation;
  const card = template.content.cloneNode(true);
  const checkbox = card.querySelector('input');
  checkbox.checked = plannedItem.isPacked;
  checkbox.addEventListener('change', async () => {
    await fetch(`/api/packing-lists/${packingListId}/items/${recommendationItem.item.id}`, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ isPacked: checkbox.checked })
    });
  });
  card.querySelector('.type').textContent = typeNames[recommendationItem.item.type];
  card.querySelector('h3').textContent = recommendationItem.item.name;
  card.querySelector('.reason').textContent = recommendationItem.reasons.join(' · ');
  card.querySelector('.score b').textContent = recommendationItem.score;
  container.append(card);
}
}

document.querySelector('#pack-all').addEventListener('click', () => document.querySelectorAll('#recommendations input:not(:checked)').forEach(input => input.click()));
tripSelect.addEventListener('change', async () => { activeTripId = tripSelect.value; await loadPlan(); });
document.querySelector('#show-trip-form').addEventListener('click', () => document.querySelector('#trip-form').hidden = !document.querySelector('#trip-form').hidden);
document.querySelector('#show-clothing-form').addEventListener('click', () => document.querySelector('#clothing-form').hidden = !document.querySelector('#clothing-form').hidden);
const tripForm = document.querySelector('#trip-form');
const clothingForm = document.querySelector('#clothing-form');

tripForm.addEventListener('submit', async event => {
  event.preventDefault(); const form = new FormData(tripForm);
  const trip = Object.fromEntries(form); trip.minimumTemperatureCelsius = Number(trip.minimumTemperatureCelsius); trip.maximumTemperatureCelsius = Number(trip.maximumTemperatureCelsius); trip.activities = [0];
  try {
    const response = await fetch('/api/trips', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(trip) });
    if (!response.ok) { showFeedback('No se ha podido crear el viaje. Revisa los datos.', true); return; }
    const created = await response.json(); tripForm.reset(); tripForm.hidden = true; await loadTrips(created.id); await loadPlan(); showFeedback(`Viaje a ${created.destination} creado.`);
  } catch { showFeedback('No se ha podido conectar con la aplicación.', true); }
});
clothingForm.addEventListener('submit', async event => {
  event.preventDefault(); const form = new FormData(clothingForm); const item = Object.fromEntries(form);
  Object.assign(item, { id: '00000000-0000-0000-0000-000000000000', type: Number(item.type), season: Number(item.season), warmthLevel: Number(item.warmthLevel), style: 0, weightGrams: Number(item.weightGrams), waterproof: false, isClean: true, isAvailable: true, preferenceScore: 70, combinesWith: [] });
  try {
    const response = await fetch('/api/wardrobe', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(item) });
    if (!response.ok) { showFeedback('No se ha podido añadir la prenda. Puede que ya exista una con ese nombre.', true); return; }
    const created = await response.json(); clothingForm.reset(); clothingForm.hidden = true; await loadWardrobe(); await loadPlan(); showFeedback(`${created.name} se ha añadido al armario.`);
  } catch { showFeedback('No se ha podido conectar con la aplicación.', true); }
});

await loadTrips();
await loadWardrobe();
await loadPlan();
