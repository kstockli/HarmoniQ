// Liefert eine stabile, anonyme Voter-ID pro Browser (für Bewertungen ohne Login).
// Kein personenbezogenes Datum – nur eine zufällige UUID im localStorage.
export function getVoterId() {
    let id = localStorage.getItem('musicrater_voter');
    if (!id) {
        id = (crypto.randomUUID && crypto.randomUUID()) ||
             ('xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
                 const r = Math.random() * 16 | 0;
                 const v = c === 'x' ? r : (r & 0x3 | 0x8);
                 return v.toString(16);
             }));
        localStorage.setItem('musicrater_voter', id);
    }
    return id;
}
